// ============================================================
// WebServer.cs
// HTTP サーバー本体
//
// 役割:
//   System.Net.HttpListener を使って軽量な HTTP サーバーを実現します。
//   iPhone の Safari から接続を受け付け、以下の API を提供します。
//
//   GET  /                    → iPhone 向け Web UI（HTML）を返す
//   GET  /api/files           → 共有フォルダのファイル一覧を JSON で返す
//   GET  /api/download/{name} → 指定ファイルをダウンロードさせる
//   POST /api/upload          → multipart/form-data でファイルを受信・保存する
//   POST /api/delete          → 指定ファイルを削除する
//
// 設計上の注意:
//   ・Start() は呼び出し元（UI スレッド）で同期的に実行します。
//     これにより HttpListenerException を MainForm 側で確実に catch できます。
//     リクエスト受付ループ（ListenLoop）は別スレッドで非同期実行します。
//   ・各リクエストは独立したタスクで並行処理されます。
//   ・multipart 解析は外部ライブラリを使わず自前実装しています。
//   ・ディレクトリトラバーサル攻撃を防ぐためファイル名を検証しています。
//   ・接続元 IP とリクエストパスをログに出力します（接続診断用）。
// ============================================================

using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace FileTransfer;

public class WebServer
{
    // -------------------------------------------------------
    // フィールド
    // -------------------------------------------------------

    /// <summary>iPhone と共有するフォルダのパス。</summary>
    private readonly string _sharedFolder;

    /// <summary>HTTP リスナーが待ち受けるポート番号。</summary>
    private readonly int _port;

    /// <summary>.NET 標準の HTTP リスナー。</summary>
    private HttpListener? _listener;

    /// <summary>リスナーループのキャンセルトークンソース。</summary>
    private CancellationTokenSource? _cts;

    // -------------------------------------------------------
    // イベント
    // -------------------------------------------------------

    /// <summary>
    /// ファイル転送やエラーなどのログメッセージを UI に通知するイベント。
    /// UI スレッドへのマーシャリングは呼び出し元（MainForm）が行います。
    /// </summary>
    public event EventHandler<string>? OnLog;

    // -------------------------------------------------------
    // コンストラクター
    // -------------------------------------------------------

    /// <summary>
    /// Web サーバーを初期化します。
    /// </summary>
    /// <param name="sharedFolder">ファイルを共有するフォルダのフルパス。</param>
    /// <param name="port">HTTP リスナーのポート番号（1024 ～ 65535）。</param>
    public WebServer(string sharedFolder, int port)
    {
        _sharedFolder = sharedFolder;
        _port = port;
    }

    // -------------------------------------------------------
    // 公開メソッド
    // -------------------------------------------------------

    /// <summary>
    /// HTTP リスナーを起動し、リクエスト受付ループをバックグラウンドで開始します。
    /// <para>
    /// "http://+:port/" のプレフィックスは全 NIC（Wi-Fi・有線 LAN など）で
    /// リッスンすることを意味し、iPhone からの接続を受け付けるために必要です。
    /// このプレフィックスを使うには管理者権限または URL ACL の事前登録が必要です。
    /// </para>
    /// <para>
    /// ★ このメソッドは呼び出し元（UI スレッド）で直接実行してください。
    ///   Task.Run 内で呼ぶと、_listener.Start() がスローする
    ///   HttpListenerException が async void 経由で消失し、
    ///   アプリがクラッシュする原因になります。
    ///   _listener.Start() 自体は瞬時に完了するため UI ブロックの問題はありません。
    ///   長時間動作するリクエスト受付ループは別スレッド（Task.Run）で実行します。
    /// </para>
    /// </summary>
    /// <exception cref="HttpListenerException">
    /// URL ACL が未登録で管理者権限がない場合にスローされます（ErrorCode = 5）。
    /// MainForm.StartServer() でこの例外を catch し、URL ACL 登録を促します。
    /// </exception>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();

        // "http://+:port/" = すべての NIC（Wi-Fi / Ethernet）でリッスンする指定
        // iPhone が同一 Wi-Fi 経由で接続できるよう、localhost ではなく + を使う
        _listener.Prefixes.Add($"http://+:{_port}/");

        // 呼び出し元（UI スレッド）で同期的に実行し、例外を直接 catch できるようにする
        _listener.Start();

        // リクエスト受付ループは長時間実行されるため別スレッドで非同期実行する
        Task.Run(() => ListenLoop(_cts.Token));
    }

    /// <summary>
    /// HTTP リスナーを停止し、リソースを解放します。
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
    }

    // -------------------------------------------------------
    // リクエスト受付ループ
    // -------------------------------------------------------

    /// <summary>
    /// キャンセルされるまでリクエストを待ち続けるループです。
    /// 受信したリクエストは HandleRequest に渡して別タスクで処理します。
    /// </summary>
    private async Task ListenLoop(CancellationToken ct)
    {
        Log("リクエスト受付ループを開始しました。接続を待機中...");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 次のリクエストが来るまで非同期で待機する（CPU を消費しない）
                var context = await _listener!.GetContextAsync();

                // 接続元 IP とリクエスト内容をログに出力する
                // iPhone が正しく接続できているか診断するために使用する
                var remoteIp = context.Request.RemoteEndPoint?.ToString() ?? "不明";
                Log($"接続: {remoteIp} → {context.Request.HttpMethod} {context.Request.Url?.AbsolutePath}");

                // リクエストごとに独立したタスクで処理することで複数同時接続に対応する
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (ObjectDisposedException) { break; }   // Stop() によるリスナー破棄時
            catch (HttpListenerException) { break; }      // Stop() 呼び出し後の待機解除時
            catch (Exception ex)
            {
                Log($"エラー: {ex.Message}");
            }
        }
    }

    // -------------------------------------------------------
    // リクエストルーティング
    // -------------------------------------------------------

    /// <summary>
    /// リクエストの URL パスと HTTP メソッドに応じて適切な処理を振り分けます。
    /// 例外発生時は 500 エラーを返し、最後に必ずレスポンスをクローズします。
    /// </summary>
    private async Task HandleRequest(HttpListenerContext context)
    {
        var request  = context.Request;
        var response = context.Response;
        var path     = request.Url?.AbsolutePath ?? "/";
        var method   = request.HttpMethod;

        try
        {
            if (path == "/" && method == "GET")
            {
                // トップページ：iPhone 向け Web UI を返す
                await ServeHtml(response);
            }
            else if (path == "/api/files" && method == "GET")
            {
                // ファイル一覧 API
                await ServeFileList(response);
            }
            else if (path.StartsWith("/api/download/") && method == "GET")
            {
                // ファイルダウンロード API（パスから URL デコードしてファイル名を取得）
                var fileName = HttpUtility.UrlDecode(path["/api/download/".Length..]);
                await ServeFile(response, fileName);
            }
            else if (path == "/api/upload" && method == "POST")
            {
                // ファイルアップロード API
                await HandleUpload(request, response);
            }
            else if (path == "/api/delete" && method == "POST")
            {
                // ファイル削除 API
                await HandleDelete(request, response);
            }
            else
            {
                // 未定義のパスは 404 を返す
                response.StatusCode = 404;
                await WriteText(response, "Not Found");
            }
        }
        catch (Exception ex)
        {
            Log($"リクエスト処理エラー [{path}]: {ex.Message}");
            try
            {
                response.StatusCode = 500;
                await WriteText(response, "Internal Server Error");
            }
            catch { }
        }
        finally
        {
            // レスポンスを必ずクローズしてソケットを解放する
            try { response.Close(); } catch { }
        }
    }

    // -------------------------------------------------------
    // HTML ページ配信
    // -------------------------------------------------------

    /// <summary>
    /// iPhone 向けの Web UI（HTML）をレスポンスとして返します。
    /// HTML の内容は HtmlContent.cs に定義されています。
    /// </summary>
    private async Task ServeHtml(HttpListenerResponse response)
    {
        response.ContentType  = "text/html; charset=utf-8";
        response.StatusCode   = 200;
        var html   = HtmlContent.GetPage();
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
    }

    // -------------------------------------------------------
    // ファイル一覧 API
    // -------------------------------------------------------

    /// <summary>
    /// 共有フォルダ内のファイル情報を JSON 配列として返します。
    /// 各ファイルの情報: name（名前）、size（バイト数）、modified（更新日時）、extension（拡張子）
    /// ファイルは更新日時の新しい順で返します。
    /// </summary>
    private async Task ServeFileList(HttpListenerResponse response)
    {
        var files = new List<object>();

        if (Directory.Exists(_sharedFolder))
        {
            foreach (var filePath in Directory.GetFiles(_sharedFolder))
            {
                var info = new FileInfo(filePath);
                files.Add(new
                {
                    name      = info.Name,
                    size      = info.Length,
                    modified  = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    extension = info.Extension.ToLowerInvariant()
                });
            }
        }

        // 更新日時の新しい順（降順）にソートする
        // dynamic を使って匿名型のプロパティにアクセスしている
        files.Sort((a, b) =>
        {
            var aTime = ((dynamic)b).modified as string;
            var bTime = ((dynamic)a).modified as string;
            return string.Compare(aTime, bTime, StringComparison.Ordinal);
        });

        response.ContentType = "application/json; charset=utf-8";
        response.StatusCode  = 200;
        var json   = JsonSerializer.Serialize(files);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
    }

    // -------------------------------------------------------
    // ファイルダウンロード API
    // -------------------------------------------------------

    /// <summary>
    /// 指定ファイルをダウンロードレスポンスとして返します。
    /// <para>
    /// セキュリティ対策:
    ///   ・".." や "/" "\" を含むファイル名を拒否（ディレクトリトラバーサル防止）
    ///   ・存在しないファイルは 404 を返す
    /// </para>
    /// <para>
    /// ファイル名の文字化け対策:
    ///   RFC 6266 に従い filename*=UTF-8''エンコード名 形式を使用します。
    ///   日本語ファイル名も正しく扱えます。
    /// </para>
    /// </summary>
    private async Task ServeFile(HttpListenerResponse response, string fileName)
    {
        // ─ セキュリティチェック：ディレクトリトラバーサル攻撃を防ぐ ─
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            response.StatusCode = 400;
            await WriteText(response, "Invalid filename");
            return;
        }

        var filePath = Path.Combine(_sharedFolder, fileName);
        if (!File.Exists(filePath))
        {
            response.StatusCode = 404;
            await WriteText(response, "File not found");
            return;
        }

        var info = new FileInfo(filePath);
        response.ContentType     = GetMimeType(info.Extension);
        response.StatusCode      = 200;
        response.ContentLength64 = info.Length;

        // Content-Disposition ヘッダーで「ダウンロード保存」を指示する
        // RFC 5987 形式（filename*=UTF-8''...）で日本語ファイル名に対応する
        var encodedName = Uri.EscapeDataString(fileName);
        response.Headers.Add("Content-Disposition",
            $"attachment; filename=\"{encodedName}\"; filename*=UTF-8''{encodedName}");

        // ファイルをストリームで読み込みレスポンスへコピーする（メモリ効率が良い）
        using var fs = File.OpenRead(filePath);
        await fs.CopyToAsync(response.OutputStream);

        Log($"ダウンロード: {fileName} ({FormatSize(info.Length)})");
    }

    // -------------------------------------------------------
    // ファイルアップロード API
    // -------------------------------------------------------

    /// <summary>
    /// multipart/form-data 形式でアップロードされたファイルを受信・保存します。
    /// <para>
    /// 処理の流れ:
    /// 1. Content-Type ヘッダーから boundary 文字列を取得する
    /// 2. リクエストボディ全体をメモリに読み込む
    /// 3. SplitMultipart でパートに分割する
    /// 4. ParsePart で各パートのヘッダーとコンテンツを抽出する
    /// 5. Content-Disposition ヘッダーからファイル名を取得する
    /// 6. 重複ファイル名は "_1", "_2" のサフィックスを付けて回避する
    /// 7. 保存したファイル名の一覧を JSON で返す
    /// </para>
    /// </summary>
    private async Task HandleUpload(HttpListenerRequest request, HttpListenerResponse response)
    {
        var contentType = request.ContentType;

        // Content-Type が multipart/form-data でなければエラー
        if (contentType == null || !contentType.Contains("multipart/form-data"))
        {
            response.StatusCode = 400;
            await WriteJson(response, new { error = "Invalid content type" });
            return;
        }

        // Content-Type から boundary を抽出する（例: "----WebKitFormBoundaryXXXX"）
        var boundary = ExtractBoundary(contentType);
        if (boundary == null)
        {
            response.StatusCode = 400;
            await WriteJson(response, new { error = "No boundary found" });
            return;
        }

        var uploadedFiles = new List<string>();

        // リクエストボディ全体をバイト配列として読み込む
        // ※ 大容量ファイル（数百 MB 以上）の場合はメモリ使用量に注意
        using var ms = new MemoryStream();
        await request.InputStream.CopyToAsync(ms);
        var body = ms.ToArray();

        // multipart データを各パートに分割して処理する
        var parts = SplitMultipart(body, boundary);
        foreach (var part in parts)
        {
            var (headers, content) = ParsePart(part);
            var disposition = headers.GetValueOrDefault("Content-Disposition", "");

            // Content-Disposition ヘッダーからファイル名を取得する
            var fileName = ExtractFileName(disposition);
            if (string.IsNullOrEmpty(fileName))
                continue;  // ファイル名がない場合（フォームフィールド等）はスキップ

            // Path.GetFileName でパス部分を除去してファイル名のみを取得する（セキュリティ対策）
            fileName = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(fileName))
                continue;

            // 同名ファイルが存在する場合はサフィックスを付けて別名保存する
            // 例: photo.jpg → photo_1.jpg → photo_2.jpg ...
            var targetPath = Path.Combine(_sharedFolder, fileName);
            if (File.Exists(targetPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var ext     = Path.GetExtension(fileName);
                var counter = 1;
                do
                {
                    targetPath = Path.Combine(_sharedFolder, $"{nameWithoutExt}_{counter}{ext}");
                    counter++;
                } while (File.Exists(targetPath));
                fileName = Path.GetFileName(targetPath);
            }

            // ファイルをディスクに書き込む
            await File.WriteAllBytesAsync(targetPath, content);
            uploadedFiles.Add(fileName);
            Log($"アップロード: {fileName} ({FormatSize(content.Length)})");
        }

        // アップロードしたファイル名の一覧を返す
        response.StatusCode = 200;
        await WriteJson(response, new { files = uploadedFiles, count = uploadedFiles.Count });
    }

    // -------------------------------------------------------
    // ファイル削除 API
    // -------------------------------------------------------

    /// <summary>
    /// JSON ボディで指定されたファイルを共有フォルダから削除します。
    /// リクエストボディ例: {"name": "photo.jpg"}
    /// </summary>
    private async Task HandleDelete(HttpListenerRequest request, HttpListenerResponse response)
    {
        // リクエストボディを読み込んでファイル名を取得する
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var json     = await reader.ReadToEndAsync();
        var doc      = JsonDocument.Parse(json);
        var fileName = doc.RootElement.GetProperty("name").GetString();

        // ─ セキュリティチェック：不正なファイル名を拒否する ─
        if (string.IsNullOrEmpty(fileName) ||
            fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            response.StatusCode = 400;
            await WriteJson(response, new { error = "Invalid filename" });
            return;
        }

        var filePath = Path.Combine(_sharedFolder, fileName);
        if (!File.Exists(filePath))
        {
            response.StatusCode = 404;
            await WriteJson(response, new { error = "File not found" });
            return;
        }

        File.Delete(filePath);
        Log($"削除: {fileName}");
        response.StatusCode = 200;
        await WriteJson(response, new { success = true });
    }

    // -------------------------------------------------------
    // multipart/form-data パーサー
    // -------------------------------------------------------

    #region Multipart Parser

    /// <summary>
    /// Content-Type ヘッダー文字列から boundary 値を抽出します。
    /// <para>
    /// 入力例: "multipart/form-data; boundary=----WebKitFormBoundaryABC123"
    /// 出力例: "----WebKitFormBoundaryABC123"
    /// </para>
    /// </summary>
    private static string? ExtractBoundary(string contentType)
    {
        var parts = contentType.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            {
                // boundary 値を取得し、前後のダブルクォートを除去する
                var boundary = trimmed["boundary=".Length..].Trim('"');
                return boundary;
            }
        }
        return null;
    }

    /// <summary>
    /// multipart ボディをバイト配列の境界で分割し、各パートのバイト配列リストを返します。
    /// <para>
    /// multipart の構造:
    ///   --boundary\r\n
    ///   ヘッダー\r\n
    ///   \r\n
    ///   コンテンツ\r\n
    ///   --boundary\r\n
    ///   ...
    ///   --boundary--\r\n  ← 終端
    /// </para>
    /// </summary>
    /// <param name="body">リクエストボディ全体のバイト配列。</param>
    /// <param name="boundary">境界文字列（Content-Type ヘッダーから取得）。</param>
    private static List<byte[]> SplitMultipart(byte[] body, string boundary)
    {
        var parts          = new List<byte[]>();
        var boundaryBytes  = Encoding.UTF8.GetBytes("--" + boundary);
        var endBoundary    = Encoding.UTF8.GetBytes("--" + boundary + "--");

        int pos = 0;

        // 最初の境界を探す
        pos = IndexOf(body, boundaryBytes, pos);
        if (pos < 0) return parts;  // 境界が見つからない場合は空リストを返す
        pos += boundaryBytes.Length;

        while (pos < body.Length)
        {
            // 境界直後の CRLF（\r\n）をスキップする
            if (pos < body.Length - 1 && body[pos] == '\r' && body[pos + 1] == '\n')
                pos += 2;

            // 次の境界を探す
            var nextPos = IndexOf(body, boundaryBytes, pos);
            if (nextPos < 0) break;

            // パートの終端（次の境界の直前）を計算する
            // 境界の直前の CRLF は除去する
            var end = nextPos;
            if (end >= 2 && body[end - 2] == '\r' && body[end - 1] == '\n')
                end -= 2;

            if (end > pos)
            {
                // パートをバイト配列としてコピーしてリストに追加する
                var part = new byte[end - pos];
                Array.Copy(body, pos, part, 0, part.Length);
                parts.Add(part);
            }

            pos = nextPos + boundaryBytes.Length;

            // 終端境界（--boundary--）に到達したらループを抜ける
            if (pos < body.Length - 1 && body[pos] == '-' && body[pos + 1] == '-')
                break;
        }

        return parts;
    }

    /// <summary>
    /// multipart の 1 パートをヘッダー辞書とコンテンツバイト配列に分解します。
    /// <para>
    /// パートの構造:
    ///   Content-Disposition: form-data; name="files"; filename="photo.jpg"\r\n
    ///   Content-Type: image/jpeg\r\n
    ///   \r\n                    ← ヘッダーとボディの区切り（\r\n\r\n）
    ///   [ファイルのバイナリデータ]
    /// </para>
    /// </summary>
    /// <returns>ヘッダー辞書（キーは大文字小文字を無視）とコンテンツのタプル。</returns>
    private static (Dictionary<string, string> headers, byte[] content) ParsePart(byte[] part)
    {
        var headers   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ヘッダーとボディの区切り "\r\n\r\n" を探す
        var headerEnd = IndexOf(part, Encoding.UTF8.GetBytes("\r\n\r\n"), 0);

        if (headerEnd < 0)
            return (headers, part);  // 区切りが見つからない場合はそのまま返す

        // ヘッダーテキストを解析して辞書に格納する
        var headerText = Encoding.UTF8.GetString(part, 0, headerEnd);
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                var key = line[..colonIdx].Trim();
                var val = line[(colonIdx + 1)..].Trim();
                headers[key] = val;
            }
        }

        // ヘッダー末尾（\r\n\r\n の後）からコンテンツを抽出する
        var contentStart = headerEnd + 4;  // "\r\n\r\n" は 4 バイト
        var content      = new byte[part.Length - contentStart];
        Array.Copy(part, contentStart, content, 0, content.Length);

        return (headers, content);
    }

    /// <summary>
    /// Content-Disposition ヘッダー文字列からファイル名を取得します。
    /// <para>
    /// 優先順位:
    /// 1. filename*= （RFC 5987 形式：エンコーディング'言語'URLエンコード値）
    ///    → 日本語ファイル名を正しく扱える
    /// 2. filename=  （従来形式）
    /// </para>
    /// <para>
    /// 入力例1: "form-data; name=\"files\"; filename*=UTF-8''%E5%86%99%E7%9C%9F.jpg"
    /// 出力例1: "写真.jpg"
    /// 入力例2: "form-data; name=\"files\"; filename=\"photo.jpg\""
    /// 出力例2: "photo.jpg"
    /// </para>
    /// </summary>
    private static string? ExtractFileName(string disposition)
    {
        // ─ 手法 1: filename*= （RFC 5987 / RFC 6266 形式）を優先する ─
        var idx = disposition.IndexOf("filename*=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var val     = disposition[(idx + "filename*=".Length)..];
            var semiIdx = val.IndexOf(';');
            if (semiIdx >= 0) val = val[..semiIdx];
            val = val.Trim();

            // 形式: "UTF-8''URLエンコードされたファイル名"
            var quoteIdx = val.IndexOf('\'');
            if (quoteIdx >= 0)
            {
                var secondQuote = val.IndexOf('\'', quoteIdx + 1);
                if (secondQuote >= 0)
                {
                    val = val[(secondQuote + 1)..];
                    return HttpUtility.UrlDecode(val);  // URL デコードして実際のファイル名を返す
                }
            }
        }

        // ─ 手法 2: 従来の filename= 形式にフォールバックする ─
        idx = disposition.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var val     = disposition[(idx + "filename=".Length)..];
            var semiIdx = val.IndexOf(';');
            if (semiIdx >= 0) val = val[..semiIdx];
            val = val.Trim().Trim('"');  // ダブルクォートを除去する
            return val;
        }

        return null;
    }

    /// <summary>
    /// バイト配列 haystack の中から needle の開始位置を返します。
    /// 見つからない場合は -1 を返します（Array.IndexOf の byte[] 版）。
    /// </summary>
    /// <param name="haystack">検索対象のバイト配列。</param>
    /// <param name="needle">検索するバイト列。</param>
    /// <param name="start">検索を開始するインデックス。</param>
    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (int i = start; i <= haystack.Length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }

    #endregion

    // -------------------------------------------------------
    // ヘルパーメソッド
    // -------------------------------------------------------

    #region Helpers

    /// <summary>
    /// プレーンテキストをレスポンスとして書き込みます。
    /// </summary>
    private static async Task WriteText(HttpListenerResponse response, string text)
    {
        response.ContentType = "text/plain; charset=utf-8";
        var buffer = Encoding.UTF8.GetBytes(text);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
    }

    /// <summary>
    /// オブジェクトを JSON シリアライズしてレスポンスとして書き込みます。
    /// </summary>
    private static async Task WriteJson(HttpListenerResponse response, object obj)
    {
        response.ContentType = "application/json; charset=utf-8";
        var json   = JsonSerializer.Serialize(obj);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
    }

    /// <summary>
    /// ファイル拡張子から適切な MIME タイプを返します。
    /// iPhone からのダウンロード時にファイルの種類を正しく認識させるために使用します。
    /// 未知の拡張子は "application/octet-stream"（汎用バイナリ）を返します。
    /// </summary>
    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".heic" or ".heif"=> "image/heic",          // iPhone のカメラ形式
        ".mp4"            => "video/mp4",
        ".mov"            => "video/quicktime",      // iPhone の動画形式
        ".pdf"            => "application/pdf",
        ".txt"            => "text/plain; charset=utf-8",
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css"            => "text/css",
        ".js"             => "application/javascript",
        ".json"           => "application/json",
        ".zip"            => "application/zip",
        ".mp3"            => "audio/mpeg",
        ".wav"            => "audio/wav",
        ".doc"            => "application/msword",
        ".docx"           => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls"            => "application/vnd.ms-excel",
        ".xlsx"           => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _                 => "application/octet-stream"
    };

    /// <summary>
    /// バイト数を人間が読みやすい形式（B / KB / MB / GB）に変換します。
    /// ログ表示に使用します。
    /// </summary>
    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024                => $"{bytes} B",
        < 1024 * 1024         => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024  => $"{bytes / (1024.0 * 1024):F1} MB",
        _                     => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    /// <summary>
    /// OnLog イベントを発火してログメッセージを通知します。
    /// </summary>
    private void Log(string message) => OnLog?.Invoke(this, message);

    #endregion
}
