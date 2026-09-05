using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NUnit.Framework;
using PocketBlaster.Networking;
using UnityEngine;

namespace PocketBlaster.Tests.EditMode
{
    /// <summary>
    /// PhoneOrientationServer(手書きのHTTPS/WebSocket(wss)実装)を、実機のスマホ無しで検証する。
    /// テスト自身が「スマホのブラウザ」役としてTCP接続→TLSハンドシェイク→WSハンドシェイク→
    /// マスク付きテキストフレーム送信を行い、サーバー側が正しく処理できるかを見る。
    /// サーバーは自己署名証明書を使うため、テスト側の証明書検証は常に受理するようにしている
    /// (スマホのブラウザで「信頼して進む」を押すのと同じ扱い)。
    /// </summary>
    public class PhoneOrientationServerTests
    {
        private const int TestPort = 17755;

        [Test]
        public void ReceivesOrientationAndReloadMessages()
        {
            var indexHtmlPath = Path.GetTempFileName();
            File.WriteAllText(indexHtmlPath, "<html></html>");

            var certificate = TestCertificate();
            var server = new PhoneOrientationServer(TestPort, indexHtmlPath, certificate);
            server.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect("127.0.0.1", TestPort);
                    using (var stream = ConnectTls(client))
                    {
                        PerformHandshake(stream);

                        SendTextFrame(stream, "{\"type\":\"orientation\",\"alpha\":10.5,\"beta\":20.5,\"gamma\":-5.5}");
                        var orientation = WaitForMessage(server, "orientation", TimeSpan.FromSeconds(3));
                        Assert.AreEqual(10.5, orientation.alpha, 0.001);
                        Assert.AreEqual(20.5, orientation.beta, 0.001);
                        Assert.AreEqual(-5.5, orientation.gamma, 0.001);

                        SendTextFrame(stream, "{\"type\":\"reload\"}");
                        var reload = WaitForMessage(server, "reload", TimeSpan.FromSeconds(3));
                        Assert.AreEqual("reload", reload.type);

                        SendTextFrame(stream, "{\"type\":\"shoot\"}");
                        var shoot = WaitForMessage(server, "shoot", TimeSpan.FromSeconds(3));
                        Assert.AreEqual("shoot", shoot.type);

                        Assert.IsTrue(server.IsClientConnected, "ハンドシェイク後は接続中と判定されるべき");
                    }
                }
            }
            finally
            {
                server.Stop();
                File.Delete(indexHtmlPath);
            }
        }

        [Test]
        public void ServesStaticHtmlForPlainGetRequest()
        {
            var indexHtmlPath = Path.GetTempFileName();
            const string marker = "<html>POCKETBLASTER_TEST_MARKER</html>";
            File.WriteAllText(indexHtmlPath, marker);

            var certificate = TestCertificate();
            var server = new PhoneOrientationServer(TestPort + 1, indexHtmlPath, certificate);
            server.Start();
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect("127.0.0.1", TestPort + 1);
                    using (var stream = ConnectTls(client))
                    {
                        var request = "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n";
                        var requestBytes = Encoding.ASCII.GetBytes(request);
                        stream.Write(requestBytes, 0, requestBytes.Length);

                        var response = ReadUntilConnectionClosed(stream);

                        StringAssert.Contains("200 OK", response);
                        StringAssert.Contains(marker, response);
                    }
                }
            }
            finally
            {
                server.Stop();
                File.Delete(indexHtmlPath);
            }
        }

        private static X509Certificate2 TestCertificate()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            return DevCertificate.LoadOrGenerate(projectRoot);
        }

        private static SslStream ConnectTls(TcpClient client)
        {
            var sslStream = new SslStream(client.GetStream(), false,
                (sender, cert, chain, errors) => true); // 自己署名なので常に受理する(スマホでの「信頼して進む」相当)
            sslStream.AuthenticateAsClient("127.0.0.1");
            return sslStream;
        }

        private static string ReadUntilConnectionClosed(Stream stream)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static PhoneOrientationServer.InboundMessage WaitForMessage(PhoneOrientationServer server, string expectedType, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (server.TryDequeue(out var msg) && msg.type == expectedType)
                {
                    return msg;
                }
                Thread.Sleep(20);
            }
            Assert.Fail($"'{expectedType}' メッセージを {timeout.TotalSeconds}秒以内に受信できませんでした");
            return default;
        }

        private static void PerformHandshake(Stream stream)
        {
            var key = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-key-0123456"));
            var request =
                "GET / HTTP/1.1\r\n" +
                "Host: 127.0.0.1\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Key: " + key + "\r\n" +
                "Sec-WebSocket-Version: 13\r\n\r\n";
            var requestBytes = Encoding.ASCII.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);

            var buffer = new byte[1024];
            var read = stream.Read(buffer, 0, buffer.Length);
            var response = Encoding.ASCII.GetString(buffer, 0, read);
            Assert.IsTrue(response.Contains("101"), "ハンドシェイクに失敗: " + response);
        }

        private static void SendTextFrame(Stream stream, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var maskKey = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            var masked = new byte[payload.Length];
            for (var i = 0; i < payload.Length; i++) masked[i] = (byte)(payload[i] ^ maskKey[i % 4]);

            using var ms = new MemoryStream();
            ms.WriteByte(0x81); // FIN + text opcode
            if (payload.Length < 126)
            {
                ms.WriteByte((byte)(0x80 | payload.Length)); // MASK bit + length
            }
            else
            {
                ms.WriteByte((byte)(0x80 | 126));
                ms.WriteByte((byte)(payload.Length >> 8));
                ms.WriteByte((byte)payload.Length);
            }
            ms.Write(maskKey, 0, 4);
            ms.Write(masked, 0, masked.Length);

            var frame = ms.ToArray();
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }
    }
}
