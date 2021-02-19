using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaHueControl
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().MainAsync().Wait();
        }

        async Task MainAsync()
        {
            var cred = await GetOrInitializeCredentials();

            ILocalHueClient client = new LocalHueClient(cred.Item1);
            client.Initialize(cred.Item2);

            var lights = await client.GetLightsAsync();
            var colorLights = lights.Where(x => x.Name.Contains("color"));
            if (colorLights.Count() != 3)
            {
                Console.WriteLine("No 3 lights???");
                return;
            }

            Console.WriteLine("Inb4...");
            Thread.Sleep(5000);

            await TurnOff(client, lights);
            Console.WriteLine("Darkness!");

            Thread.Sleep(3000);

            await SetColors(client, colorLights, new RGBColor("FF0000"), new RGBColor("FF0000"), new RGBColor("FF0000"));
            Console.WriteLine("After Boot");
            Thread.Sleep(2000);

            await SetColors(client, colorLights, new RGBColor("0000FF"), new RGBColor("0000FF"), new RGBColor("0000FF"));
            Console.WriteLine("After Swap1");
            Thread.Sleep(2000);

            await SetColors(client, colorLights, new RGBColor("FF00FF"), new RGBColor("FF00FF"), new RGBColor("FF00FF"));
            Console.WriteLine("After Swap2");
            Thread.Sleep(2000);

            while (true)
            {
                await SetColors(client, colorLights, new RGBColor("FF20FF"), new RGBColor("FF20FF"), new RGBColor("FF20FF"));
                Thread.Sleep(1000);
            }
        }

        private async Task TurnOff(ILocalHueClient client, IEnumerable<Light> lights)
        {
            var command = new LightCommand
            {
                On = false
            };
            await client.SendCommandAsync(command, lights.Select(x => x.Id));
        }

        private async Task SetColors(ILocalHueClient client, IEnumerable<Light> lights, RGBColor color1, RGBColor color2, RGBColor color3, bool effect = false)
        {
            var command1 = new LightCommand
            {
                On = true,
                Brightness = 255,
                Effect = effect ? Effect.ColorLoop : Effect.None
            };
            command1.SetColor(color1);

            var command2 = new LightCommand
            {
                On = true,
                Brightness = 255,
                Effect = effect ? Effect.ColorLoop : Effect.None
            };
            command2.SetColor(color2);

            var command3 = new LightCommand
            {
                On = true,
                Brightness = 255,
                Effect = effect ? Effect.ColorLoop : Effect.None
            };
            command3.SetColor(color3);
            await Task.WhenAll(client.SendCommandAsync(command1, new string[] { lights.ElementAt(0).Id }),
                client.SendCommandAsync(command2, new string[] { lights.ElementAt(1).Id }),
                client.SendCommandAsync(command3, new string[] { lights.ElementAt(2).Id }));
        }


        private async Task RandomizeStuff(ILocalHueClient client, IEnumerable<Light> colorLights)
        {
            var command = new LightCommand
            {
                On = true,
                Brightness = 255,
                Effect = Effect.None
            };
            List<long[]> highs = new List<long[]>();
            foreach (var light in colorLights)
            {
                long[] high = GetRandomHighs(highs);
                highs.Add(high);
                string colorstr = GetRandomColor(high).ToString("X6");
                command.SetColor(new RGBColor(colorstr));
                await client.SendCommandAsync(command, new string[] { light.Id });
            }
            //await client.SendCommandAsync(command, colorLights.Select(x => x.Id));
        }

        private static long[] GetRandomHighs(List<long[]> previous)
        {
            again:
            long[] highsarr = new long[3];
            highsarr[0] = GetRandom(0, 2);
            highsarr[1] = GetRandom(0, 2);
            highsarr[2] = GetRandom(0, 2);
            long highs = highsarr.Sum();
            if (highs == 0 || highs == 3 || previous.Where(x => (x[0] == highsarr[0]) && (x[1] == highsarr[1]) && (x[2] == highsarr[2])).Count() > 0)
            {
                goto again;
            }
            return highsarr;
        }

        private static int width = 32;

        private static long GetRandomColor(long[] high)
        {
            long result = 0;

            for (int i = 0; i < 3; i++)
            {
                long subcolor;
                if (high[i] == 0)
                {
                    subcolor = GetRandom(0, width);
                }
                else
                {
                    subcolor = GetRandom(256 - width, 256);
                }
                result |= subcolor << (i * 8);
            }
            return result;
        }

        private static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        public static long GetRandom(long loBound, long hiBound)
        {
            byte[] data = new byte[8];
            rng.GetBytes(data);
            ulong randLong = BitConverter.ToUInt64(data, 0);
            return loBound + (long)(randLong % (ulong)(hiBound - loBound));
        }

        private async Task<Tuple<string, string>> GetOrInitializeCredentials()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "haparty.dat");
            if (File.Exists(path))
            {
                var data = File.ReadAllLines(path);
                return Tuple.Create(data[0], data[1]);
            }
            else
            {
                var result = await GetBridgeDetails();
                File.WriteAllLines(path, new string[] { result.Item1, result.Item2 });
                return result;
            }
        }

        private async Task<Tuple<string, string>> GetBridgeDetails()
        {
            IBridgeLocator locator = new HttpBridgeLocator();

            //For Windows 8 and .NET45 projects you can use the SSDPBridgeLocator which actually scans your network. 
            //See the included BridgeDiscoveryTests and the specific .NET and .WinRT projects
            var bridgeIPs = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5));
            var ip = bridgeIPs.First().IpAddress;

            ILocalHueClient client = new LocalHueClient(ip);
            string appKey;
            tryagain:
            try
            {
                appKey = await client.RegisterAsync("HaParty", Environment.MachineName);
            }
            catch (Exception e)
            {
                Thread.Sleep(500);
                goto tryagain;
            }
            //Save the app key for later use

            return Tuple.Create(ip, appKey);
        }
    }
}
