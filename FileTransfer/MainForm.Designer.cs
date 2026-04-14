// ============================================================
// MainForm.Designer.cs
// メインウィンドウのコントロール定義（デザイナー生成コード）
//
// 役割:
//   Visual Studio のフォームデザイナーが管理するコントロールの
//   生成・配置・プロパティ設定を行います。
//   このファイルは通常デザイナーが自動生成しますが、
//   手動で編集することも可能です。
//
// コントロール構成:
//   [設定グループ]
//     lblFolder / txtFolder / btnBrowse / btnOpenFolder  ... 共有フォルダ
//     lblPort   / nudPort                                ... ポート番号
//     btnStartStop                                       ... サーバー開始/停止
//     lblUrl    / txtUrl    / btnCopyUrl                 ... 接続URL表示
//   [転送ロググループ]
//     lstLog                                             ... ログ一覧
//     btnClearLog                                        ... ログクリア
// ============================================================

namespace FileTransfer;

partial class MainForm
{
    /// <summary>
    /// 使用中のデザイナー変数。Dispose メソッドで破棄されます。
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// 使用中のリソースをすべてクリーンアップします。
    /// </summary>
    /// <param name="disposing">マネージドリソースを破棄する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// デザイナーサポートに必要なメソッドです。
    /// コードエディターで内容を変更しないでください。
    /// </summary>
    private void InitializeComponent()
    {
        // ── コントロールのインスタンス生成 ──────────────────────
        grpSettings = new GroupBox();
        lblFolder   = new Label();
        txtFolder   = new TextBox();
        btnBrowse   = new Button();
        lblPort     = new Label();
        nudPort     = new NumericUpDown();
        btnStartStop = new Button();
        lblUrl      = new Label();
        txtUrl      = new TextBox();
        btnCopyUrl  = new Button();
        grpLog      = new GroupBox();
        lstLog      = new ListBox();
        btnClearLog = new Button();
        btnOpenFolder = new Button();

        // レイアウト更新を一時停止（パフォーマンス向上のため）
        grpSettings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)nudPort).BeginInit();
        grpLog.SuspendLayout();
        SuspendLayout();

        // ── 設定グループボックス ──────────────────────────────
        grpSettings.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpSettings.Controls.Add(lblFolder);
        grpSettings.Controls.Add(txtFolder);
        grpSettings.Controls.Add(btnBrowse);
        grpSettings.Controls.Add(lblPort);
        grpSettings.Controls.Add(nudPort);
        grpSettings.Controls.Add(btnStartStop);
        grpSettings.Controls.Add(lblUrl);
        grpSettings.Controls.Add(txtUrl);
        grpSettings.Controls.Add(btnCopyUrl);
        grpSettings.Controls.Add(btnOpenFolder);
        grpSettings.Location = new Point(12, 12);
        grpSettings.Size     = new Size(660, 150);
        grpSettings.Text     = "設定";

        // ── 共有フォルダラベル ────────────────────────────────
        lblFolder.AutoSize = true;
        lblFolder.Location = new Point(10, 28);
        lblFolder.Text     = "共有フォルダ:";

        // ── 共有フォルダパステキストボックス（読み取り専用）──────
        txtFolder.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtFolder.Location = new Point(100, 25);
        txtFolder.Size     = new Size(430, 23);
        txtFolder.ReadOnly = true;  // ユーザーが直接編集できないようにする

        // ── フォルダ選択ボタン（「...」） ─────────────────────
        btnBrowse.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowse.Location = new Point(536, 24);
        btnBrowse.Size     = new Size(40, 25);
        btnBrowse.Text     = "...";
        btnBrowse.Click   += BtnBrowse_Click;

        // ── エクスプローラーで開くボタン ─────────────────────
        btnOpenFolder.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenFolder.Location = new Point(580, 24);
        btnOpenFolder.Size     = new Size(70, 25);
        btnOpenFolder.Text     = "開く";
        btnOpenFolder.Click   += BtnOpenFolder_Click;

        // ── ポート番号ラベル ──────────────────────────────────
        lblPort.AutoSize = true;
        lblPort.Location = new Point(10, 62);
        lblPort.Text     = "ポート:";

        // ── ポート番号スピンボタン（1024 ～ 65535、デフォルト 8080）
        nudPort.Location = new Point(100, 60);
        nudPort.Minimum  = 1024;    // ウェルノウンポートを避けるため 1024 以上に制限
        nudPort.Maximum  = 65535;
        nudPort.Value    = 8080;    // HTTP の代替ポートとして一般的な 8080 をデフォルトに
        nudPort.Size     = new Size(80, 23);

        // ── サーバー開始 / 停止ボタン ─────────────────────────
        btnStartStop.Location = new Point(200, 58);
        btnStartStop.Size     = new Size(120, 28);
        btnStartStop.Text     = "サーバー開始";
        btnStartStop.Click   += BtnStartStop_Click;

        // ── 接続 URL ラベル ──────────────────────────────────
        lblUrl.AutoSize = true;
        lblUrl.Location = new Point(10, 98);
        lblUrl.Text     = "接続URL:";

        // ── 接続 URL テキストボックス（読み取り専用・等幅フォントで視認性向上）
        txtUrl.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtUrl.Location = new Point(100, 95);
        txtUrl.Size     = new Size(470, 23);
        txtUrl.ReadOnly = true;
        txtUrl.Font     = new Font("Consolas", 10F, FontStyle.Bold);  // URL を見やすくする

        // ── URL コピーボタン ──────────────────────────────────
        btnCopyUrl.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
        btnCopyUrl.Location = new Point(580, 94);
        btnCopyUrl.Size     = new Size(70, 25);
        btnCopyUrl.Text     = "コピー";
        btnCopyUrl.Click   += BtnCopyUrl_Click;

        // ── 転送ロググループボックス ──────────────────────────
        grpLog.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        grpLog.Controls.Add(lstLog);
        grpLog.Controls.Add(btnClearLog);
        grpLog.Location = new Point(12, 170);
        grpLog.Size     = new Size(660, 280);
        grpLog.Text     = "転送ログ";

        // ── ログ一覧リストボックス ────────────────────────────
        lstLog.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lstLog.Location          = new Point(10, 22);
        lstLog.Size              = new Size(640, 220);
        lstLog.HorizontalScrollbar = true;           // 長いパスを横スクロールで確認できるようにする
        lstLog.Font              = new Font("Consolas", 9F);  // 等幅フォントでログを整列表示

        // ── ログクリアボタン ──────────────────────────────────
        btnClearLog.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClearLog.Location = new Point(570, 248);
        btnClearLog.Size     = new Size(80, 25);
        btnClearLog.Text     = "クリア";
        btnClearLog.Click   += BtnClearLog_Click;

        // ── メインフォーム本体 ────────────────────────────────
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode       = AutoScaleMode.Font;
        ClientSize          = new Size(684, 461);
        Controls.Add(grpSettings);
        Controls.Add(grpLog);
        MinimumSize         = new Size(500, 400);   // リサイズしても UI が崩れないよう最小サイズを設定
        Text                = "File Transfer - iPhone ⇔ PC";
        FormClosing        += MainForm_FormClosing;
        Load               += MainForm_Load;

        // レイアウト更新を再開する
        grpSettings.ResumeLayout(false);
        grpSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)nudPort).EndInit();
        grpLog.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    // ── コントロール宣言 ─────────────────────────────────────
    private GroupBox       grpSettings;    // 設定グループボックス
    private Label          lblFolder;      // 「共有フォルダ:」ラベル
    private TextBox        txtFolder;      // 共有フォルダパス表示
    private Button         btnBrowse;      // フォルダ選択ボタン（「...」）
    private Label          lblPort;        // 「ポート:」ラベル
    private NumericUpDown  nudPort;        // ポート番号入力スピン
    private Button         btnStartStop;   // サーバー開始/停止ボタン
    private Label          lblUrl;         // 「接続URL:」ラベル
    private TextBox        txtUrl;         // 接続 URL 表示テキストボックス
    private Button         btnCopyUrl;     // URL コピーボタン
    private GroupBox       grpLog;         // 転送ロググループボックス
    private ListBox        lstLog;         // 転送ログ一覧
    private Button         btnClearLog;    // ログクリアボタン
    private Button         btnOpenFolder;  // エクスプローラーで開くボタン
}
