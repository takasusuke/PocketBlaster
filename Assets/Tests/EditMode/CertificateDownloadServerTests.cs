using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NUnit.Framework;
using PocketBlaster.Networking;
using UnityEngine;

namespace PocketBlaster.Tests.EditMode
{
    /// <summary>
    /// CertificateDownloadServerが、平文HTTPで証明書の公開部分を正しく配布できるかを検証する。
    /// iOSへのプロファイルインストール用の入り口なので、暗号化なしで正しいバイト列が
    /// 返ることが重要(TLSの警告を経由せずに配布する、というこの仕組みの前提)。
    /// </summary>
    public class CertificateDownloadServerTests
    {
        private const int TestPort = 17766;

        [Test]
        public void ServesCertificatePublicBytesOverPlainHttp()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var certificate = DevCertificate.LoadOrGenerate(projectRoot);
            var expectedBytes = certificate.Export(X509ContentType.Cert);

            var server = new CertificateDownloadServer(TestPort, certificate);
            server.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect("127.0.0.1", TestPort);
                    using (var stream = client.GetStream())
                    {
                        var request = "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n";
                        var requestBytes = Encoding.ASCII.GetBytes(request);
                        stream.Write(requestBytes, 0, requestBytes.Length);

                        var responseBytes = ReadUntilConnectionClosed(stream);
                        // ヘッダー部分は常にASCIIで書いているため、境界(\r\n\r\n)を探すだけなら
                        // ASCIIデコードで十分(本文のバイナリ部分がここで文字化けしても問題ない)。
                        var responseText = Encoding.ASCII.GetString(responseBytes);

                        var headerEnd = responseText.IndexOf("\r\n\r\n");
                        Assert.Greater(headerEnd, 0, "レスポンスにヘッダー終端が見つからない");
                        var headerText = responseText.Substring(0, headerEnd);
                        StringAssert.Contains("200 OK", headerText);
                        StringAssert.Contains("application/x-x509-ca-cert", headerText);

                        var bodyBytes = new byte[responseBytes.Length - (headerEnd + 4)];
                        System.Array.Copy(responseBytes, headerEnd + 4, bodyBytes, 0, bodyBytes.Length);

                        Assert.AreEqual(expectedBytes, bodyBytes, "証明書の公開部分のバイト列が一致しない");
                    }
                }
            }
            finally
            {
                server.Stop();
            }
        }

        private static byte[] ReadUntilConnectionClosed(NetworkStream stream)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }
}
