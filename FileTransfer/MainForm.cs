// ============================================================
// MainForm.cs
// メインウィンドウのロジック
//
// 役割:
//   アプリケーションの UI ロジックを担当します。
//   ・共有フォルダの選択
//   ・ポート番号の設定
//   ・Web サーバーの開始 / 停止
//   ・接続 URL の表示とクリップボードへのコピー
//   ・転送ログの表示
//
// 初回起動時の自動処理:
//   ・URL ACL 登録（HttpListener がポートをリッスンするための権限）
//   ・Windows ファイアウォール受信許可ルールの追加
//   いずれも UAC ダイアログ経由で管理者権限を取得して実行します。
//   2回目以降はすでに設定済みのためスキップされます。
// ============================================================

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FileTransfer;

public partial class MainForm : Form
{
    // -------------------------------------------------------
    // フィールド
    // -------------------------------------------------------

    /// <summary>現在動作中の Web サーバーインスタンス。停止中は null。</summary>
    private WebServer? _server;

    /// <summary>サーバーが起動中かどうかを示すフラグ。</summary>
    private bool _running;

    // -------------------------------------------------------
    // コンストラクター
    // -------------------------------------------------------

    public MainForm()
    {
        // Designer.cs で生成されたコントロールを初期化する
        InitializeComponent();
    }

    // -------------------------------------------------------
    // フォームイベント
    // -------------------------------------------------------

    /// <summary>
    /// フォーム読み込み時の初期化処理。
    /// デフォルトの共有フォルダ（マイドキュメント\FileTransfer）を設定します。
    /// フォルダが存在しない場合は自動的に作成します。
    /// </summary>
    private void MainForm_Load(object sender, EventArgs e)
    {
        // デフォルトの共有フォルダパスを「マイドキュメント\FileTransfer」に設定
        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FileTransfer");

        // フォルダが存在しなければ作成する
        Directory.CreateDirectory(defaultFolder);
        txtFolder.Text = defaultFolder;

        AddLog("アプリケーション起動。「サーバー開始」を押してファイル転送を開始してください。");
    }

    /// <summary>
    /// アプリケーション終了時にサーバーを確実に停止します。
    /// </summary>
    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        _server?.Stop();
    }

    // -------------------------------------------------------
    // ボタンイベント
    // -------------------------------------------------------

    /// <summary>
    /// 「...」ボタン：フォルダ選択ダイアログを開きます。
    /// 選択されたフォルダパスをテキストボックスに反映します。
    /// </summary>
    private void BtnBrowse_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "共有するフォルダを選択してください",
            SelectedPath = txtFolder.Text,   // 現在のパスをダイアログの初期位置にする
            ShowNewFolderButton = true        // 新規フォルダ作成ボタンを表示する
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtFolder.Text = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// 「開く」ボタン：エクスプローラーで共有フォルダを開きます。
    /// </summary>
    private void BtnOpenFolder_Click(object sender, EventArgs e)
    {
        if (Directory.Exists(txtFolder.Text))
        {
            Process.Start("explorer.exe", txtFolder.Text);
        }
    }

    /// <summary>
    /// 「サーバー開始 / 停止」ボタン：サーバーの起動・停止を切り替えます。
    /// <para>
    /// 以前は async void でしたが、StartServer() 内で発生する
    /// HttpListenerException が async void 経由では catch できなかったため、
    /// 同期メソッドに変更しました。
    /// _listener.Start() は瞬時に完了するため UI のブロックは問題ありません。
    /// </para>
    /// </summary>
    private void BtnStartStop_Click(object sender, EventArgs e)
    {
        if (!_running)
        {
            StartServer();
        }
        else
        {
            StopServer();
        }
    }

    /// <summary>
    /// 「コピー」ボタン：接続 URL をクリップボードにコピーします。
    /// </summary>
    private void BtnCopyUrl_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(txtUrl.Text))
        {
            Clipboard.SetText(txtUrl.Text);
            AddLog("URLをクリップボードにコピーしました。");
        }
    }

    /// <summary>
    /// 「クリア」ボタン：転送ログ一覧をすべて消去します。
    /// </summary>
    private void BtnClearLog_Click(object sender, EventArgs e)
    {
        lstLog.Items.Clear();
    }

    // -------------------------------------------------------
    // サーバー制御
    // -------------------------------------------------------

    /// <summary>
    /// Web サーバーを起動します。
    /// <para>
    /// 手順:
    /// 1. 共有フォルダの存在確認
    /// 2. WebServer インスタンスを生成し、UI スレッド上で直接 Start() を呼び出す
    /// 3. アクセス拒否（ErrorCode=5）の場合は URL ACL 登録（netsh）を試みて再起動
    /// 4. 起動成功後にファイアウォールルールを追加する
    /// 5. UI を「サーバー稼働中」状態に切り替える
    /// </para>
    /// <para>
    /// ★ Task.Run で Start() を呼ばない理由:
    ///   以前は await Task.Run(() => _server.Start()) としていましたが、
    ///   Task.Run 内でスローされた HttpListenerException が async void
    ///   イベントハンドラー経由では catch ブロックに届かず、
    ///   未処理例外としてアプリがクラッシュしていました。
    ///   _listener.Start() は瞬時に終わるため UI スレッドで直接呼んでも問題ありません。
    /// </para>
    /// </summary>
    private void StartServer()
    {
        var folder = txtFolder.Text;

        // 共有フォルダが実際に存在するか確認する
        if (!Directory.Exists(folder))
        {
            MessageBox.Show("共有フォルダが存在しません。", "エラー",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var port = (int)nudPort.Value;

        // WebServer インスタンスを生成し、ログイベントを UI スレッドで受け取る
        _server = new WebServer(folder, port);
        _server.OnLog += (_, msg) => BeginInvoke(() => AddLog(msg));

        try
        {
            // UI スレッド上で直接 Start() を呼ぶことで
            // HttpListenerException を確実に catch できるようにする
            _server.Start();
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            // エラーコード 5 = ACCESS_DENIED
            // Windows の HttpListener で http://+:ポート/ をリッスンするには
            // 管理者権限または URL ACL の事前登録が必要なため、その対処を行う
            var result = MessageBox.Show(
                $"ポート {port} でのリッスンに管理者権限が必要です。\n\n" +
                $"自動でURL予約を追加しますか？\n" +
                $"（UACの確認ダイアログが表示されます）",
                "アクセス拒否",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // UAC 経由で netsh を実行して URL ACL を登録する
                if (RegisterUrlAcl(port))
                {
                    try
                    {
                        // ACL 登録後に再度サーバーを起動する
                        _server.Start();
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show($"サーバー起動に失敗しました:\n{ex2.Message}",
                            "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    return; // ACL 登録に失敗した場合は中断する
                }
            }
            else
            {
                return; // ユーザーがキャンセルした場合は中断する
            }
        }
        catch (Exception ex)
        {
            // ポート競合など、その他のエラー
            MessageBox.Show($"サーバー起動に失敗しました:\n{ex.Message}",
                "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // サーバー起動成功 → iPhone から接続できるようファイアウォールルールを追加する
        // （既に登録済みの場合はスキップされる）
        AddFirewallRule(port);

        // UI を稼働中の状態に切り替える
        _running = true;
        btnStartStop.Text = "サーバー停止";

        // サーバー稼働中は設定変更を禁止する
        txtFolder.Enabled = false;
        btnBrowse.Enabled = false;
        nudPort.Enabled = false;

        // iPhone に表示するための接続 URL を生成して表示する
        // GetLocalIpAddress() で Wi-Fi の NIC に割り当てられた IP を取得する
        var ip = GetLocalIpAddress();
        var url = $"http://{ip}:{port}/";
        txtUrl.Text = url;
        AddLog($"サーバー起動: {url}");
        AddLog($"リッスン中: http://+:{port}/（全 NIC 対象）");
        AddLog("iPhoneのSafariで上記URLにアクセスしてください。");
    }

    /// <summary>
    /// Web サーバーを停止し、UI を初期状態に戻します。
    /// </summary>
    private void StopServer()
    {
        _server?.Stop();
        _server = null;
        _running = false;
        btnStartStop.Text = "サーバー開始";

        // 設定変更を再度許可する
        txtFolder.Enabled = true;
        btnBrowse.Enabled = true;
        nudPort.Enabled = true;
        txtUrl.Text = "";

        AddLog("サーバー停止。");
    }

    // -------------------------------------------------------
    // ユーティリティ
    // -------------------------------------------------------

    /// <summary>
    /// 転送ログにタイムスタンプ付きのメッセージを追加します。
    /// 最新のエントリーが常に表示されるよう自動スクロールします。
    /// </summary>
    private void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lstLog.Items.Add(line);

        // 最新行が見えるよう最下部にスクロールする
        lstLog.TopIndex = lstLog.Items.Count - 1;
    }

    /// <summary>
    /// 同一 LAN 上でアクセス可能なローカル IP アドレスを取得します。
    /// <para>
    /// 手法 1（優先）: UDP ソケットを外部 IP（8.8.8.8）に向けて接続し、
    ///   OS がルーティングに使用するローカル IP を取得します（実際の通信は発生しません）。
    /// 手法 2（フォールバック）: DNS から自ホストのエントリーを取得します。
    /// どちらも失敗した場合は "localhost" を返します。
    /// </para>
    /// </summary>
    private static string GetLocalIpAddress()
    {
        // 手法 1: ダミーの UDP 接続でルーティングに使用される NIC の IP を特定する
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);  // 実際のパケット送信は行われない
            if (socket.LocalEndPoint is IPEndPoint endPoint)
                return endPoint.Address.ToString();
        }
        catch { }

        // 手法 2: DNS からホスト名に対応する IPv4 アドレスを取得する
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }

        return "localhost";
    }

    /// <summary>
    /// Windows ファイアウォールに受信許可ルールを追加します。
    /// iPhoneなど外部デバイスからのHTTP接続を許可するために必要です。
    /// UAC の確認ダイアログが表示されます。
    /// </summary>
    /// <param name="port">許可するポート番号。</param>
    private void AddFirewallRule(int port)
    {
        var ruleName = $"FileTransfer_Port{port}";
        try
        {
            // 既にルールが存在するか確認する
            var checkPsi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var checkProc = Process.Start(checkPsi);
            var output = checkProc?.StandardOutput.ReadToEnd() ?? "";
            checkProc?.WaitForExit(5000);

            // ルールが既に存在する場合はスキップする
            if (checkProc?.ExitCode == 0 && output.Contains(ruleName))
            {
                AddLog($"ファイアウォールルール '{ruleName}' は既に登録済みです。");
                return;
            }

            // ファイアウォールルールを追加する（管理者権限が必要）
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(10000);
            if (proc?.ExitCode == 0)
            {
                AddLog($"ファイアウォールルール '{ruleName}' を追加しました。");
            }
            else
            {
                AddLog("警告: ファイアウォールルールの追加に失敗しました。iPhoneから接続できない可能性があります。");
            }
        }
        catch (Exception ex)
        {
            AddLog($"警告: ファイアウォール設定をスキップしました: {ex.Message}");
        }
    }

    /// <summary>
    /// netsh コマンドを管理者権限（UAC）で実行し、
    /// 指定ポートへの HttpListener アクセス権を現在のユーザーに付与します。
    /// <para>
    /// 実行するコマンド例:
    ///   netsh http add urlacl url=http://+:8080/ user=DOMAIN\UserName
    /// </para>
    /// </summary>
    /// <param name="port">アクセス権を付与するポート番号。</param>
    /// <returns>登録成功なら true、失敗またはキャンセルなら false。</returns>
    private static bool RegisterUrlAcl(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"http add urlacl url=http://+:{port}/ user={Environment.UserDomainName}\\{Environment.UserName}",
                Verb = "runas",             // 管理者として実行（UAC プロンプトを表示）
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(10000);       // 最大 10 秒待機
            return proc?.ExitCode == 0;     // 終了コード 0 = 成功
        }
        catch
        {
            return false;
        }
    }
}
