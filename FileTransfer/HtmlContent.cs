// ============================================================
// HtmlContent.cs
// iPhone 向け Web UI（HTML / CSS / JavaScript）
//
// 役割:
//   Safari で表示する Web ページの内容を C# の文字列定数として保持します。
//   外部ファイルを使わず実行ファイル（exe）内に埋め込むことで、
//   単一ファイルで動作させることができます。
//
// Web ページの機能:
//   ・ファイルアップロード（写真・動画・その他 / 複数選択対応 / プログレスバー付き）
//   ・共有ファイル一覧表示（画像はサムネイルプレビュー付き）
//   ・ファイルダウンロード
//   ・ファイル削除
//   ・5 秒ごとの自動更新
//
// デザイン:
//   ・Apple Human Interface Guidelines に準拠したカラーパレット
//   ・iPhone Safari に最適化したモバイルファーストレイアウト
//   ・タッチ操作に配慮した大きなタップターゲット
// ============================================================

namespace FileTransfer;

public static class HtmlContent
{
    /// <summary>
    /// iPhone Safari 向け Web UI の HTML 文字列を返します。
    /// C# 11 の生文字列リテラル（"""..."""）を使用しています。
    /// </summary>
    public static string GetPage() => """
<!DOCTYPE html>
<html lang="ja">
<head>
<meta charset="UTF-8">
<!-- iPhone Safari のビューポート設定。ユーザーによるズームを無効化して固定レイアウトにする -->
<meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
<!-- ホーム画面に追加した場合にフルスクリーンで動作させる -->
<meta name="apple-mobile-web-app-capable" content="yes">
<title>File Transfer</title>
<style>
/* ── CSS 変数（カラーパレット）────────────────────────────
   Apple のシステムカラーに合わせた配色定義 */
:root {
    --primary: #007AFF;      /* Apple ブルー（ボタン・リンク等） */
    --primary-dark: #0056CC; /* プレス時のボタン色 */
    --danger: #FF3B30;       /* 削除ボタン等の警告色 */
    --success: #34C759;      /* 成功メッセージ用（現在未使用） */
    --bg: #F2F2F7;           /* システム背景色 */
    --card: #FFFFFF;         /* カード背景色 */
    --text: #1C1C1E;         /* メインテキスト色 */
    --text2: #8E8E93;        /* サブテキスト色 */
    --border: #E5E5EA;       /* 区切り線・ボーダー色 */
    --radius: 12px;          /* カードの角丸半径 */
}

/* ── リセット & 基本設定 ─────────────────────────────── */
* { margin:0; padding:0; box-sizing:border-box; }
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
    background: var(--bg);
    color: var(--text);
    min-height: 100vh;
    -webkit-text-size-adjust: 100%; /* iOS での自動フォントサイズ変更を無効化 */
}

/* ── ヘッダー（スクロール時も固定表示） ─────────────── */
.header {
    background: var(--primary);
    color: white;
    padding: 16px 20px;
    text-align: center;
    position: sticky;  /* スクロールしても上部に固定する */
    top: 0;
    z-index: 100;
    box-shadow: 0 2px 8px rgba(0,0,0,0.15);
}
.header h1 { font-size: 20px; font-weight: 600; }
.header .subtitle { font-size: 12px; opacity: 0.8; margin-top: 2px; }

/* ── メインコンテナ（最大幅を設定してタブレットでも見やすくする） */
.container { padding: 16px; max-width: 600px; margin: 0 auto; }

/* ── カード（セクションの外枠） ─────────────────────── */
.card {
    background: var(--card);
    border-radius: var(--radius);
    padding: 20px;
    margin-bottom: 16px;
    box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}
.card h2 {
    font-size: 17px;
    font-weight: 600;
    margin-bottom: 14px;
    display: flex;
    align-items: center;
    gap: 8px;
}

/* ── アップロードエリア（ドラッグ&ドロップ / タップ） ── */
.upload-area {
    border: 2px dashed var(--border);
    border-radius: var(--radius);
    padding: 30px 16px;
    text-align: center;
    transition: all 0.2s;
    cursor: pointer;
}
/* ドラッグオーバー時のスタイル変化 */
.upload-area.dragover {
    border-color: var(--primary);
    background: rgba(0,122,255,0.05);
}
.upload-area .icon { font-size: 40px; margin-bottom: 8px; }
.upload-area p { color: var(--text2); font-size: 14px; }
/* 実際の file input は非表示にして見た目はカスタムUIで提供する */
.upload-area input[type=file] { display: none; }

/* ── ボタン基本スタイル ──────────────────────────────── */
.btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    padding: 10px 20px;
    border: none;
    border-radius: 8px;
    font-size: 15px;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s;
    -webkit-tap-highlight-color: transparent; /* タップ時のハイライトを無効化 */
}
/* アップロードボタン（全幅） */
.btn-primary {
    background: var(--primary);
    color: white;
    width: 100%;
    margin-top: 12px;
}
.btn-primary:active { background: var(--primary-dark); }  /* タップ時に暗くする */
.btn-primary:disabled { background: var(--text2); }        /* 無効時はグレーにする */
/* 小さいボタン（ダウンロード・削除） */
.btn-small {
    padding: 6px 14px;
    font-size: 13px;
    border-radius: 6px;
}
.btn-download { background: var(--primary); color: white; }
.btn-delete   { background: var(--danger);  color: white; }

/* ── プログレスバー ──────────────────────────────────── */
.progress-bar {
    width: 100%;
    height: 6px;
    background: var(--border);
    border-radius: 3px;
    margin-top: 12px;
    overflow: hidden;
    display: none; /* アップロード中のみ表示する */
}
.progress-bar .fill {
    height: 100%;
    background: var(--primary);
    border-radius: 3px;
    transition: width 0.3s; /* スムーズに進捗を更新する */
    width: 0%;
}
.progress-text {
    font-size: 13px;
    color: var(--text2);
    margin-top: 6px;
    text-align: center;
    display: none; /* アップロード中のみ表示する */
}

/* ── 選択ファイルの表示チップ ───────────────────────── */
.selected-files { margin-top: 10px; font-size: 13px; color: var(--text); }
.selected-files .file-chip {
    display: inline-block;
    background: var(--bg);
    padding: 4px 10px;
    border-radius: 14px;
    margin: 3px 4px 3px 0;
    font-size: 12px;
}

/* ── ファイル一覧 ────────────────────────────────────── */
.file-list { list-style: none; }
.file-item {
    display: flex;
    align-items: center;
    padding: 12px 0;
    border-bottom: 1px solid var(--border);
    gap: 12px;
}
.file-item:last-child { border-bottom: none; } /* 最後の区切り線を非表示にする */

/* ── ファイルアイコン（種類別にカラーコーディング） ───── */
.file-icon {
    width: 40px;
    height: 40px;
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
    flex-shrink: 0; /* 幅が狭くても縮まないようにする */
}
.file-icon.img   { background: #FFF3E0; } /* 画像：オレンジ系 */
.file-icon.vid   { background: #E8F5E9; } /* 動画：グリーン系 */
.file-icon.doc   { background: #E3F2FD; } /* 文書：ブルー系 */
.file-icon.other { background: #F3E5F5; } /* その他：パープル系 */

/* ── ファイル情報テキスト ────────────────────────────── */
.file-info { flex: 1; min-width: 0; } /* min-width: 0 でテキストの折り返しを正しく動作させる */
.file-name {
    font-size: 14px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis; /* 長いファイル名は「...」で省略する */
}
.file-meta { font-size: 12px; color: var(--text2); margin-top: 2px; }
.file-actions { display: flex; gap: 6px; flex-shrink: 0; }

/* ── 空状態の表示 ────────────────────────────────────── */
.empty-state {
    text-align: center;
    padding: 24px;
    color: var(--text2);
    font-size: 14px;
}

/* ── トースト通知（画面下部に一時表示） ─────────────── */
.toast {
    position: fixed;
    bottom: 30px;
    left: 50%;
    transform: translateX(-50%) translateY(100px); /* 初期は画面外に配置 */
    background: rgba(0,0,0,0.85);
    color: white;
    padding: 12px 24px;
    border-radius: 25px;
    font-size: 14px;
    transition: transform 0.3s ease;
    z-index: 200;
    white-space: nowrap;
}
/* show クラスを付与すると画面内にスライドインする */
.toast.show { transform: translateX(-50%) translateY(0); }

/* ── 更新ボタン ──────────────────────────────────────── */
.refresh-btn {
    background: none;
    border: none;
    color: var(--primary);
    font-size: 14px;
    cursor: pointer;
    padding: 4px 8px;
}
.file-count { font-size: 13px; color: var(--text2); font-weight: 400; }

/* ── 画像サムネイル ──────────────────────────────────── */
.preview-thumb {
    width: 40px;
    height: 40px;
    border-radius: 8px;
    object-fit: cover; /* アスペクト比を保ちながらトリミングして正方形に収める */
    flex-shrink: 0;
}
</style>
</head>
<body>

<!-- ヘッダー：スクロールしても常に上部に固定 -->
<div class="header">
    <h1>File Transfer</h1>
    <div class="subtitle">iPhone &#8596; PC</div>
</div>

<div class="container">

    <!-- ════════════════════════════════════════════════════
         アップロードセクション
         iPhone → PC へファイルを送信する
         ════════════════════════════════════════════════════ -->
    <div class="card">
        <h2>&#128228; ファイルを送信</h2>

        <!-- アップロードエリア：タップで file input を開く / PC からはドラッグ&ドロップ可 -->
        <div class="upload-area" id="uploadArea" onclick="fileInput.click()">
            <div class="icon">&#128206;</div>
            <p>タップしてファイルを選択<br><small>写真・動画・その他のファイル</small></p>
            <!-- multiple: 複数ファイル選択を許可する -->
            <input type="file" id="fileInput" multiple>
        </div>

        <!-- 選択されたファイルのチップ表示エリア -->
        <div class="selected-files" id="selectedFiles"></div>

        <!-- アップロードボタン（ファイル未選択時は disabled） -->
        <button class="btn btn-primary" id="uploadBtn" onclick="uploadFiles()" disabled>
            &#128640; アップロード
        </button>

        <!-- プログレスバー（アップロード中のみ表示） -->
        <div class="progress-bar" id="progressBar">
            <div class="fill" id="progressFill"></div>
        </div>
        <!-- 進捗テキスト（例: "45% (4.5 MB / 10.0 MB)"） -->
        <div class="progress-text" id="progressText"></div>
    </div>

    <!-- ════════════════════════════════════════════════════
         共有ファイル一覧セクション
         PC → iPhone へファイルをダウンロードする
         ════════════════════════════════════════════════════ -->
    <div class="card">
        <h2>
            &#128193; 共有ファイル
            <span class="file-count" id="fileCount"></span>
            <span style="flex:1"></span>
            <!-- 手動更新ボタン（5秒ごとに自動更新もされる） -->
            <button class="refresh-btn" onclick="loadFiles()">&#8635; 更新</button>
        </h2>
        <ul class="file-list" id="fileList">
            <li class="empty-state">読み込み中...</li>
        </ul>
    </div>

</div>

<!-- トースト通知（操作完了時に画面下部に表示） -->
<div class="toast" id="toast"></div>

<script>
// ────────────────────────────────────────────────────────
// DOM 要素の参照を取得する
// ────────────────────────────────────────────────────────
const fileInput      = document.getElementById('fileInput');
const uploadBtn      = document.getElementById('uploadBtn');
const selectedFilesDiv = document.getElementById('selectedFiles');
const progressBar    = document.getElementById('progressBar');
const progressFill   = document.getElementById('progressFill');
const progressText   = document.getElementById('progressText');
const fileList       = document.getElementById('fileList');
const fileCount      = document.getElementById('fileCount');
const uploadArea     = document.getElementById('uploadArea');

// ────────────────────────────────────────────────────────
// ファイル選択イベント
// ファイル input の値が変わったら選択済みファイルを表示する
// ────────────────────────────────────────────────────────
fileInput.addEventListener('change', () => {
    showSelectedFiles();
});

// ────────────────────────────────────────────────────────
// ドラッグ & ドロップ（PC のブラウザからの操作用）
// ────────────────────────────────────────────────────────
// ドラッグオーバー時のビジュアルフィードバック
uploadArea.addEventListener('dragover', e => {
    e.preventDefault(); // デフォルトの動作（ファイルを開く）を防ぐ
    uploadArea.classList.add('dragover');
});
uploadArea.addEventListener('dragleave', () => uploadArea.classList.remove('dragover'));
uploadArea.addEventListener('drop', e => {
    e.preventDefault();
    uploadArea.classList.remove('dragover');
    fileInput.files = e.dataTransfer.files; // ドロップされたファイルを input に設定する
    showSelectedFiles();
});

/**
 * 選択されたファイルをチップとして表示し、アップロードボタンを有効化する。
 */
function showSelectedFiles() {
    const files = fileInput.files;
    if (!files || files.length === 0) {
        selectedFilesDiv.innerHTML = '';
        uploadBtn.disabled = true;
        return;
    }
    uploadBtn.disabled = false;
    let html = '';
    for (const f of files) {
        // ファイル名と容量をチップ形式で表示する
        html += `<span class="file-chip">${escHtml(f.name)} (${formatSize(f.size)})</span>`;
    }
    selectedFilesDiv.innerHTML = html;
}

/**
 * 選択されたファイルを multipart/form-data で POST アップロードする。
 * XMLHttpRequest を使うことで upload.onprogress によるリアルタイム進捗表示が可能。
 * （fetch API では進捗が取得できないため XHR を使用）
 */
async function uploadFiles() {
    const files = fileInput.files;
    if (!files || files.length === 0) return;

    // UI を「アップロード中」状態にする
    uploadBtn.disabled = true;
    progressBar.style.display = 'block';
    progressText.style.display = 'block';
    progressFill.style.width = '0%';
    progressText.textContent = 'アップロード中...';

    // FormData にファイルを追加する（第 3 引数でファイル名を明示的に設定）
    const formData = new FormData();
    for (const f of files) {
        formData.append('files', f, f.name);
    }

    try {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/api/upload');

        // アップロード進捗イベントでプログレスバーを更新する
        xhr.upload.onprogress = e => {
            if (e.lengthComputable) {
                const pct = Math.round(e.loaded / e.total * 100);
                progressFill.style.width = pct + '%';
                progressText.textContent = `${pct}% (${formatSize(e.loaded)} / ${formatSize(e.total)})`;
            }
        };

        // XHR の完了を Promise でラップして await で待てるようにする
        await new Promise((resolve, reject) => {
            xhr.onload = () => {
                if (xhr.status === 200) {
                    const result = JSON.parse(xhr.responseText);
                    showToast(`${result.count}件のファイルをアップロードしました`);
                    resolve();
                } else {
                    reject(new Error('Upload failed'));
                }
            };
            xhr.onerror = () => reject(new Error('Network error'));
            xhr.send(formData);
        });

        // アップロード完了後の後処理
        fileInput.value = '';          // ファイル選択をリセットする
        selectedFilesDiv.innerHTML = '';
        progressFill.style.width = '100%';
        progressText.textContent = '完了!';
        setTimeout(() => {
            progressBar.style.display = 'none';
            progressText.style.display = 'none';
        }, 2000); // 2 秒後にプログレスバーを隠す
        loadFiles(); // ファイル一覧を再読み込みする
    } catch (err) {
        showToast('アップロードに失敗しました');
        progressText.textContent = 'エラー';
    }
    uploadBtn.disabled = false;
}

/**
 * /api/files から共有ファイル一覧を取得して表示する。
 * 画像ファイルはサムネイルプレビューを表示する。
 * 5 秒ごとに自動で呼び出される。
 */
async function loadFiles() {
    try {
        const res   = await fetch('/api/files');
        const files = await res.json();
        fileCount.textContent = `(${files.length}件)`;

        if (files.length === 0) {
            fileList.innerHTML = '<li class="empty-state">ファイルがありません</li>';
            return;
        }

        let html = '';
        for (const f of files) {
            const iconInfo    = getFileIcon(f.extension);
            const encodedName = encodeURIComponent(f.name); // URL に使用するためエンコードする

            // 画像ファイルはサムネイル表示、それ以外はアイコン表示
            const isImage = ['.jpg','.jpeg','.png','.gif','.webp','.bmp'].includes(f.extension);
            const thumb   = isImage
                ? `<img class="preview-thumb" src="/api/download/${encodedName}" loading="lazy" alt="">`
                : `<div class="file-icon ${iconInfo.cls}">${iconInfo.icon}</div>`;

            html += `<li class="file-item">
                ${thumb}
                <div class="file-info">
                    <div class="file-name">${escHtml(f.name)}</div>
                    <div class="file-meta">${formatSize(f.size)} | ${f.modified}</div>
                </div>
                <div class="file-actions">
                    <!-- download 属性を付けることでブラウザにダウンロード保存させる -->
                    <a class="btn btn-small btn-download" href="/api/download/${encodedName}" download="${escAttr(f.name)}">&#8595;</a>
                    <button class="btn btn-small btn-delete" onclick="deleteFile('${escJs(f.name)}')">&#10005;</button>
                </div>
            </li>`;
        }
        fileList.innerHTML = html;
    } catch (err) {
        fileList.innerHTML = '<li class="empty-state">読み込みに失敗しました</li>';
    }
}

/**
 * 確認ダイアログを表示してからファイルを削除する。
 * @param {string} name - 削除するファイル名
 */
async function deleteFile(name) {
    if (!confirm(`「${name}」を削除しますか?`)) return;
    try {
        const res = await fetch('/api/delete', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({name})
        });
        if (res.ok) {
            showToast('削除しました');
            loadFiles(); // 一覧を更新する
        }
    } catch (err) {
        showToast('削除に失敗しました');
    }
}

/**
 * 拡張子からファイルの種類を判定してアイコン情報を返す。
 * @param {string} ext - 拡張子（例: ".jpg"）
 * @returns {{icon: string, cls: string}} - Unicode アイコンと CSS クラス名
 */
function getFileIcon(ext) {
    const imgExts = ['.jpg','.jpeg','.png','.gif','.webp','.heic','.heif','.bmp','.svg'];
    const vidExts = ['.mp4','.mov','.avi','.mkv','.m4v'];
    const docExts = ['.pdf','.doc','.docx','.xls','.xlsx','.ppt','.pptx','.txt','.csv'];

    if (imgExts.includes(ext)) return { icon: '\u{1F5BC}', cls: 'img' };  // 🖼
    if (vidExts.includes(ext)) return { icon: '\u{1F3AC}', cls: 'vid' };  // 🎬
    if (docExts.includes(ext)) return { icon: '\u{1F4C4}', cls: 'doc' };  // 📄
    return { icon: '\u{1F4CE}', cls: 'other' };                            // 📎
}

/**
 * バイト数を人間が読みやすい単位に変換する。
 * @param {number} bytes - バイト数
 * @returns {string} - 例: "1.5 MB"
 */
function formatSize(bytes) {
    if (bytes < 1024)            return bytes + ' B';
    if (bytes < 1024*1024)       return (bytes/1024).toFixed(1) + ' KB';
    if (bytes < 1024*1024*1024)  return (bytes/(1024*1024)).toFixed(1) + ' MB';
    return (bytes/(1024*1024*1024)).toFixed(1) + ' GB';
}

/**
 * 文字列を HTML 安全な形式にエスケープする（XSS 対策）。
 * innerHTML に直接セットする場合に必ず使用すること。
 */
function escHtml(s) {
    const d = document.createElement('div');
    d.textContent = s;
    return d.innerHTML;
}
/** HTML 属性値内のダブルクォートをエスケープする（XSS 対策）。 */
function escAttr(s) { return s.replace(/"/g, '&quot;'); }
/** JavaScript 文字列リテラル内でのエスケープ（onclick 属性内の文字列に使用）。 */
function escJs(s) { return s.replace(/\\/g, '\\\\').replace(/'/g, "\\'"); }

/**
 * 画面下部にトースト通知を 2.5 秒間表示する。
 * @param {string} msg - 表示するメッセージ
 */
function showToast(msg) {
    const t = document.getElementById('toast');
    t.textContent = msg;
    t.classList.add('show');          // CSS トランジションでスライドインする
    setTimeout(() => t.classList.remove('show'), 2500);
}

// ────────────────────────────────────────────────────────
// 初期化 & 自動更新
// ────────────────────────────────────────────────────────

// ページ読み込み時にファイル一覧を取得する
loadFiles();

// 5 秒ごとに自動的にファイル一覧を更新する
// （PC 側でファイルを追加・削除した場合も自動的に反映される）
setInterval(loadFiles, 5000);
</script>
</body>
</html>
""";
}
