using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Helpers;

namespace fiskaltrust.Launcher.Extensions
{
    internal record ECDiffieHellmanDto
    {
        public required byte[] D { get; init; }
        public required byte[] QX { get; init; }
        public required byte[] QY { get; init; }
        public required byte[] PrivateKeyEC { get; init; }
        public required byte[] PrivateKeyPkcs { get; init; }
        public required byte[] PublicKey { get; init; }
    }

    static class ECDiffieHellmanExt
    {
        public static string Serialize(this ECDiffieHellman curve)
        {
            var ecParameters = curve.ExportParameters(true);

            return JsonSerializer.Serialize(new ECDiffieHellmanDto
            {
                D = ecParameters.D!,
                QX = ecParameters.Q.X!,
                QY = ecParameters.Q.Y!,
                PrivateKeyEC = curve.ExportECPrivateKey(),
                PrivateKeyPkcs = curve.ExportPkcs8PrivateKey(),
                PublicKey = curve.ExportSubjectPublicKeyInfo()
            });
        }

        public static ECDiffieHellman Deserialize(string json)
        {
            var curveJson = JsonSerializer.Deserialize<ECDiffieHellmanDto>(json)!;
            var ecParameters = new ECParameters
            {
                Curve = CashboxConfigEncryption.CURVE,
                D = curveJson.D,
                Q = new ECPoint { X = curveJson.QX, Y = curveJson.QY }
            };
            var curve = ECDiffieHellman.Create(ecParameters);
            curve.ImportSubjectPublicKeyInfo(curveJson.PublicKey, out _);
            curve.ImportECPrivateKey(curveJson.PrivateKeyEC, out _);
            curve.ImportPkcs8PrivateKey(curveJson.PrivateKeyPkcs, out _);

            return curve;
        }
    }
}