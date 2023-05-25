using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Helpers;

namespace fiskaltrust.Launcher.Extensions
{
    internal record ECDiffieHellmanDto
    {
        public ECDiffieHellmanDto(byte[] d, byte[] qx, byte[] qy, byte[] privateKeyEC, byte[] privateKeyPkcs, byte[] publicKey)
        {
            D = d;
            QX = qx; QY = qy;
            PrivateKeyEC = privateKeyEC;
            PublicKey = publicKey;
            PrivateKeyPkcs = privateKeyPkcs;
        }

        public byte[] D { get; init; }
        public byte[] QX { get; init; }
        public byte[] QY { get; init; }
        public byte[] PrivateKeyEC { get; init; }
        public byte[] PrivateKeyPkcs { get; init; }
        public byte[] PublicKey { get; init; }
    }

    static class ECDiffieHellmanExt
    {
        public static string Serialize(this ECDiffieHellman curve)
        {
            var ecParameters = curve.ExportParameters(true);

            return JsonSerializer.Serialize(new ECDiffieHellmanDto
            (
                ecParameters.D!,
                ecParameters.Q.X!,
                ecParameters.Q.Y!,
                curve.ExportECPrivateKey(),
                curve.ExportPkcs8PrivateKey(),
                curve.ExportSubjectPublicKeyInfo()
            ));
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