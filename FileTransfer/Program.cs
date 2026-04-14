// ============================================================
// Program.cs
// アプリケーションのエントリーポイント
//
// 役割:
//   Windows フォームアプリケーションの起動処理を行います。
//   STAThread 属性により、COM コンポーネント（ファイルダイアログ等）が
//   正しく動作するシングルスレッドアパートメントモードで起動します。
// ============================================================

namespace FileTransfer;

static class Program
{
    /// <summary>
    /// アプリケーションのメインエントリーポイント。
    /// DPI スケーリング等の初期設定を行い、メインフォームを起動します。
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 高 DPI 対応・フォント設定等のアプリケーション共通設定を初期化する
        ApplicationConfiguration.Initialize();

        // メインフォーム（MainForm）を生成してアプリケーションを開始する
        Application.Run(new MainForm());
    }
}
