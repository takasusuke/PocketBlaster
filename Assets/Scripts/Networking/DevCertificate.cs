using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace PocketBlaster.Networking
{
    /// <summary>
    /// ローカル開発用の自己署名TLS証明書を読み込む(無ければ生成する)。
    ///
    /// UnityのMono runtimeは証明書の"生成"(CertificateRequest.CreateSelfSigned)を
    /// サポートしておらず、実機でPlatformNotSupportedExceptionを確認した(2026-09-05)。
    /// そのため生成自体はフルの.NET SDKで動く別ツール(tools/gen-dev-cert)に任せ、
    /// ここでは結果のpfxファイルを読み込むだけにする — 証明書の読み込みはMonoでも
    /// 問題なく動く(生成と違い、ASN.1の署名処理を新たに行わないため)。
    ///
    /// 生成した証明書はプロジェクト直下の`dev-cert.pfx`にキャッシュする(.gitignore済み、
    /// マシン・ネットワークに固有のため共有しない)。IPアドレスが変わって古くなった場合は
    /// このファイルを削除すれば次回起動時に新しく生成し直される。
    /// </summary>
    public static class DevCertificate
    {
        private const string CacheFileName = "dev-cert.pfx";

        /// <param name="projectRootPath">Application.dataPathの一つ上(プロジェクトルート)</param>
        public static X509Certificate2 LoadOrGenerate(string projectRootPath)
        {
            var cachePath = Path.Combine(projectRootPath, CacheFileName);
            if (!File.Exists(cachePath))
            {
                GenerateInto(projectRootPath, cachePath);
            }

            var bytes = File.ReadAllBytes(cachePath);
            return new X509Certificate2(bytes, (string)null, X509KeyStorageFlags.Exportable);
        }

        private static void GenerateInto(string projectRootPath, string outputPath)
        {
            var toolProjectDir = Path.Combine(projectRootPath, "tools", "gen-dev-cert");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{toolProjectDir}\" -- \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("dotnetプロセスを起動できませんでした(PATHにdotnetがあるか確認してください)");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    $"開発用証明書の生成に失敗しました(tools/gen-dev-cert, exit {process.ExitCode})\n" +
                    $"stdout: {stdout}\nstderr: {stderr}");
            }

            UnityEngine.Debug.Log($"[DevCertificate] 自己署名証明書を新規生成しました: {outputPath}\n{stdout}");
        }
    }
}
