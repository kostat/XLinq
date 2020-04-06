using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Streamx.Linq.SQL.EFCore {
    // ReSharper disable once InconsistentNaming
    partial class XLinq {
        private static async Task Validate(String key) {

            var tempDir = Path.GetTempPath();
            var tokenFile = Path.Combine(tempDir, "streamx.session");
            var fingerprint = GetFingerprint();

            try {
#if NETFRAMEWORK
                var existingToken = File.ReadAllText(tokenFile);
#else
                var existingToken = await File.ReadAllTextAsync(tokenFile);
#endif
                if (ValidateToken(existingToken, fingerprint))
                    return;
            }
            catch (IOException) {
                // continue validating
            }

            var payload = GetPayload(key, fingerprint);

            using (var client = new HttpClient {BaseAddress = new Uri("https://api.cryptlex.com/v3/")}) {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try {
                    var service = key != null ? "activations" : "trial-activations";
                    response = await client.PostAsync(service, content);
                }
                catch (HttpRequestException) {
                    return;
                }

                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode) {
                    var field = key != null ? "activationToken" : "trialActivationToken";

                    var match = Regex.Match(result, getPattern(field));
                    var token = getMatch(match);

                    if (!ValidateToken(token, fingerprint))
                        return; // Hmmm

                    try {
                        var tempFile = Path.GetTempFileName();
#if NETFRAMEWORK
                        File.WriteAllText(tempFile, token);
#else
                        await File.WriteAllTextAsync(tempFile, token);
#endif
                        File.Copy(tempFile, tokenFile, true);
                        File.Delete(tempFile);
                    }
                    catch (IOException) {
                        // ignore
                    }
                }
                else {
                    var match = Regex.Match(result, getPattern("message"));
                    var message = getMatch(match);

                    match = Regex.Match(result, getPattern("code"));
                    var code = getMatch(match);

                    throw new SecurityTokenValidationException($"Error loading XLinq: {message} Reason: {code}");
                }
            }

            string getPattern(string field) => "\"" + field + "\"\\s*:\\s*\"([^\\\"]+)\"";
            string getMatch(Match match) => match.Success ? match.Groups[1].Value : null;
        }

        private static bool ValidateToken(String token,
            String fingerprint) {
            var rsa = RSA.Create();
            // rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PEM).AsSpan(), out _);
            rsa.FromXmlString(PEM_XML);

            var param = new TokenValidationParameters {
                RequireExpirationTime = false,
                ValidateAudience = false,
                ValidateIssuer = false,
                IssuerSigningKey = new RsaSecurityKey(rsa),
            };

            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var claims = handler.ValidateToken(token, param, out _);

            var fp = claims.FindFirst("fp").Value;
            if (!fingerprint.Equals(fp))
                return false;

            var leat = claims.FindFirst("leat") ?? claims.FindFirst("eat");
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(leat.Value)).Add(TimeSpan.FromHours(1));
            var expired = DateTimeOffset.UtcNow >= expiresAt;
            return !expired;
        }

        // private const String PEM = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAteuLzI/1WNVjJYZ2GjvB"
        //                            + "s7PZSfaWaFHSzYC2I7tdK37RaKmF4C7Vy31gZbwrsOvs3PuBgXsCeJVRcX76staN"
        //                            + "1yqA0DvOjS+GX44LTc/YR+Z83g4ZGmY7i08k9D8crVDIh5BtHoPGRi60Pzm7F/GS"
        //                            + "Dj9tPRpgYIajK3gjk+L5x9oq87AkiMPFOf1nxTTUho/w8qM083+7l6aAZUowixDn"
        //                            + "j2bvrKj63+LofUt2/dNNsLKuWajk+Y+FxAxIIS0y3j1kAW1+6YupDBRzqHH/SC5y"
        //                            + "WGx2vsWVOewWhQhkLXFvjtMax1mvo0mZwiTAv4QvY6gLAlO5xARRyyZl/zePgj+A" + "PQIDAQAB";
        
        private const String PEM_XML =
            "<RSAKeyValue><Modulus>teuLzI/1WNVjJYZ2GjvBs7PZSfaWaFHSzYC2I7tdK37RaKmF4C7Vy31gZbwrsOvs3PuBgXsCeJVRcX76staN1yqA0DvOjS+GX44LTc/YR+Z83g4ZGmY7i08k9D8crVDIh5BtHoPGRi60Pzm7F/GSDj9tPRpgYIajK3gjk+L5x9oq87AkiMPFOf1nxTTUho/w8qM083+7l6aAZUowixDnj2bvrKj63+LofUt2/dNNsLKuWajk+Y+FxAxIIS0y3j1kAW1+6YupDBRzqHH/SC5yWGx2vsWVOewWhQhkLXFvjtMax1mvo0mZwiTAv4QvY6gLAlO5xARRyyZl/zePgj+APQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private static bool IsSiteLocalAddress(Span<byte> ip) {
            switch (ip[0]) {
                case 10:
                    // case 127:
                    return true;
                case 172:
                    return ip[1] >= 16 && ip[1] < 32;
                case 192:
                    return ip[1] == 168;
                default:
                    return false;
            }
        }

        private static readonly char[] HEX_ARRAY = "0123456789abcdef".ToCharArray();

        private static String BytesToHex(Span<byte> bytes) {
            var hexChars = new char[bytes.Length * 3];
            for (int j = 0; j < bytes.Length; j++) {
                int v = bytes[j] & 0xFF;
                hexChars[j * 3] = HEX_ARRAY[v >> 4];
                hexChars[j * 3 + 1] = HEX_ARRAY[v & 0x0F];
                hexChars[j * 3 + 2] = ':';
            }

            return new String(hexChars, 0, hexChars.Length - 1);
        }

        private static bool IsRunningInsideDocker() {
            try {
                return File.ReadLines("/proc/self/cgroup").Any(line => line.Contains("/docker"));
            }
            catch (IOException) {
                return false;
            }
        }

        private static String GetPayload(String key,
            String fingerprint) {

            var localHost = Dns.GetHostName();

            String os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                os = "linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                os = "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                os = "mac";
            else {
                os = "ios";
            }

            var payload = JsonBuilder.Start();

            if (key != null)
                payload = payload.String("key", key);

            var docker = IsRunningInsideDocker();
            if (docker)
                payload.String("vmName", localHost);

            return payload.String("hostname", localHost)
                .String("os", os)
                .String("fingerprint", fingerprint)
                .String("appVersion", "XLinq")
                .String("userHash", Environment.UserName)
                .String("productId", "ad65ea0b-45af-43e1-b87c-4bd48f01f78a")
                .End();
        }

        private static String GetFingerprint() {
            Span<byte> ipBytes = stackalloc byte[4];

            var addresses = new List<string>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {

                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                var physicalAddress = nic.GetPhysicalAddress();
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse, PossibleUnintendedReferenceComparison
                if (physicalAddress == null || physicalAddress == PhysicalAddress.None)
                    continue;

                var addressBytes = physicalAddress.GetAddressBytes();
                var pp = nic.GetIPProperties().UnicastAddresses;

                foreach (var p in pp) {
                    var a = p.Address;
                    if (a.AddressFamily != AddressFamily.InterNetwork)
                        continue;

#if NETFRAMEWORK
                    ipBytes = a.GetAddressBytes();
#else
                    if (!a.TryWriteBytes(ipBytes, out var written) || written != 4)
                        continue;
#endif
                    
                    if (!IsSiteLocalAddress(ipBytes))
                        continue;

                    addresses.Add(BytesToHex(addressBytes));
                    addresses.Add(a.ToString());
                    break;
                }
            }

            addresses.Sort();
            addresses.Add(Dns.GetHostName());

            var fingerprint = $"[{String.Join(", ", addresses)}]";

            if (fingerprint.Length < 64) {
                var filler = new char[64 - fingerprint.Length];
                Array.Fill(filler, '=');
                fingerprint += new String(filler);
            }

            return fingerprint;
        }

        private class JsonBuilder {
            private JsonBuilder() { }

            private readonly StringBuilder builder = new StringBuilder("{");

            public static JsonBuilder Start() {
                return new JsonBuilder();
            }

            public String End() {
                int lastChar = builder.Length - 1;
                if (builder[lastChar] == ',')
                    builder[lastChar] = '}';
                else
                    builder.Append('}');
                return builder.ToString();
            }

            public JsonBuilder String(String key,
                String value) {
                builder.Append('"').Append(key).Append('"').Append(':');
                builder.Append('"').Append(value).Append('"').Append(',');
                return this;
            }
        }

        private static void ReportLicenseOk() {

            PrintBanner("Thank you for using XLinq!");
        }

        private static void ReportNoLicense() {
            PrintBanner("Thank you for using XLinq!\nNo valid XLinq license file was found.\nWelcome to trial period.");
        }

        private static void PrintBanner(String message) {
            var max = message.Split('\n').Max(s => s.Length);

            var dashes = new char[max];
            dashes.Fill('#');
            Console.WriteLine(dashes);
            Console.WriteLine();
            Console.WriteLine(message);
            Console.WriteLine();
            Console.WriteLine(dashes);
            Console.WriteLine();
        }
    }
}
