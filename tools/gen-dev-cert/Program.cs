// PocketBlasterのローカル開発用サーバー(PhoneOrientationServer)が使う自己署名TLS証明書を
// 生成するだけの使い捨てツール。UnityのMono runtimeはCertificateRequestによる証明書
// "生成"をサポートしていない(PlatformNotSupportedExceptionで実機確認済み、2026-09-05)ため、
// フルの.NET SDKでここだけ生成し、Unity側は結果のpfxファイルを読み込むだけにする。
//
// 実行: dotnet run --project tools/gen-dev-cert -- <出力先pfxパス>

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

var outputPath = args.Length > 0 ? args[0] : "dev-cert.pfx";

var ipAddresses = new List<IPAddress> { IPAddress.Loopback };
try
{
    var host = Dns.GetHostEntry(Dns.GetHostName());
    foreach (var addr in host.AddressList)
    {
        if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
        {
            ipAddresses.Add(addr);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"警告: ローカルIPの取得に失敗しました: {ex.Message}");
}

using var rsa = RSA.Create(2048);
var request = new CertificateRequest(
    "CN=PocketBlaster Dev Server",
    rsa,
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);

var sanBuilder = new SubjectAlternativeNameBuilder();
foreach (var ip in ipAddresses)
{
    sanBuilder.AddIpAddress(ip);
}
request.CertificateExtensions.Add(sanBuilder.Build());
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
request.CertificateExtensions.Add(new X509KeyUsageExtension(
    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
var notAfter = DateTimeOffset.UtcNow.AddYears(2);
using var cert = request.CreateSelfSigned(notBefore, notAfter);

var pfxBytes = cert.Export(X509ContentType.Pfx);
File.WriteAllBytes(outputPath, pfxBytes);

Console.WriteLine($"自己署名証明書を書き出しました: {Path.GetFullPath(outputPath)}");
Console.WriteLine("含めたIPアドレス:");
foreach (var ip in ipAddresses)
{
    Console.WriteLine($"  {ip}");
}
