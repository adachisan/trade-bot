using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Binance exchange functionalities by adachisan.
/// <para>https://binance-docs.github.io/apidocs/spot/en/</para>
/// <para>https://binance-docs.github.io/apidocs/futures/en/</para>
/// <para>https://github.com/binance/binance-spot-api-docs</para>
/// </summary>
namespace Binance
{
    /// <summary>Order sides.</summary>
    public enum Side { BUY, SELL }

    /// <summary>Margin type on Binance Futures.</summary>
    public enum Margin { ISOLATED, CROSSED }

    /// <summary>Binance base class.</summary>
    public abstract class Base
    {
        /// <summary>Store public key and secret key for api authentication.</summary>
        public (string key, string secret) login { get; set; } = ("", "");

        /// <summary>Default root of Binance spot api.</summary>
        public virtual string root { get; set; } = "/api/v3/";

        /// <summary>Returns '{}' if public request is successful.</summary>
        public string getTest()
        {
            return Public<string>($"https://www.binance.com{root}ping");
        }

        /// <summary>Returns '{}' if private request is successful.</summary>
        public string postTest()
        {
            string param = "symbol=BTCUSDT&side=SELL&type=MARKET&quantity=1";
            return Private<string>($"https://www.binance.com{root}order/test", param, "POST");
        }

        /// <summary>Returns true if client's timestamp is synchronized with server. 
        /// <para>Client cannot post to server if not synchronized.</para>
        /// <para>If returns false try to sync client's clock to solve the problem.</para>
        /// </summary>
        public bool TimeTest()
        {
            var read = (JsonElement x) => Float(x.GetProperty("serverTime"));
            float serverTime = read(Public<JsonElement>($"https://www.binance.com{root}time"));
            return (serverTime - TimeStamp.Unix) <= 5000;
        }

        /// <summary>Returns account balance of a specific coin.</summary>
        public virtual float balance(string coin)
        {
            return Float(account().balances.First(i => coin.Contains($"{i.asset}")).free);
        }

        /// <summary>Close all open orders of a specific coin.
        /// <para>If pair is not specify, it will close all open orders.</para>
        /// </summary>
        public virtual string close(string pair = "")
        {
            string param = pair != "" ? $"symbol={pair}" : "";
            return Private<string>($"https://www.binance.com{root}openOrders", param, "DELETE");
        }

        /// <summary>Returns information of a specific pair.
        /// <para>In case pair is not available on Binance it will return null.</para>
        /// </summary>
        public Info info(string pair)
        {
            return Public<Info>($"https://www.binance.com{root}exchangeInfo?symbol={pair}");
        }

        /// <summary>Returns information of user's account.</summary>
        public Account account()
        {
            root = root.Replace("v1", "v2");
            return Private<Account>($"https://www.binance.com{root}account", "", "GET");
        }

        /// <summary>Returns 24h brief of a specific pair.</summary>
        public Ticker ticker(string pair)
        {
            return Public<Ticker>($"https://www.binance.com{root}ticker/24hr?symbol={pair}");
        }

        /// <summary>Returns the book order of a specific pair.</summary>
        public Book book(string pair, int length)
        {
            return Public<Book>($"https://www.binance.com{root}depth?symbol={pair}&limit={length}");
        }

        /// <summary>Returns all open orders of a specific pair.
        /// <para>If pair is not specify, it will return all open orders of all pairs.</para>
        /// </summary>
        public Order[] orders(string pair = "")
        {
            string param = pair != "" ? $"symbol={pair}" : "";
            return Private<Order[]>($"https://www.binance.com{root}openOrders", param, "GET");
        }

        /// <summary>Returns market history by pair.</summary>
        public Order[] history(string pair, int length)
        {
            return Public<Order[]>($"https://www.binance.com{root}trades?symbol={pair}&limit={length}");
        }

        /// <summary>Returns acccount's history of orders by pair.</summary>
        public virtual Order[] trades(string pair, int length)
        {
            string param = $"symbol={pair}&limit={length}";
            return Private<Order[]>($"https://www.binance.com{root}myTrades", param, "GET");
        }

        /// <summary>Returns a specific order information by pair and id.</summary>
        public Order query(string pair, string id)
        {
            string param = $"symbol={pair}&orderId={id}";
            return Private<Order>($"https://www.binance.com{root}order", param, "GET");
        }

        /// <summary>Cancel a specific order by pair and id.</summary>
        public Order cancel(string pair, string id)
        {
            string param = $"symbol={pair}&orderId={id}";
            return Private<Order>($"https://www.binance.com{root}order", param, "DELETE");
        }

        /// <summary>Returns chart information of a specific pair.
        /// <para>Intervals: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w, 1M.</para>
        /// </summary>
        public Chart chart(string pair, string interval = "5m", int length = 100)
        {
            string param = $"?symbol={pair}&interval={interval}&limit={length}";
            var x = Public<object[][]>($"https://www.binance.com{root}klines{param}");
            if (x == null) return null; else Array.Reverse(x); return new() { data = x };
        }

        /// <summary>Binance public REST call.</summary>
        protected T Public<T>(string url) => REST.Call<T>(url);

        /// <summary>Binance authenticated REST call.</summary>
        protected T Private<T>(string url, string param, string type)
        {
            if (login == ("", "")) throw new("Public key and secret key not set.");
            param = $"timestamp={TimeStamp.Unix}{(param != "" ? $"&{param}" : "")}";
            param = $"{url}?{param}&signature={Hash.SHA256(param, login.secret)}";
            return REST.Call<T>(param, type, null, i => i.Headers.Add("X-MBX-APIKEY", login.key));
        }

        /// <summary>Convert any object to float.</summary>
        protected float Float<T>(T x) => float.Parse($"{x}".Replace('.', ','));
    }

    /// <summary>Binance spot class.</summary>
    public class Spot : Base
    {
        /// <summary>Creates a limit order on Binance spot.</summary>
        public Order limit(string pair, float price, float amount, Side side)
        {
            string param = $"symbol={pair}&side={side}&type=LIMIT&timeInForce=GTC&price={price}&quantity={amount}";
            return Private<Order>($"https://www.binance.com{root}order", param.Replace(",", "."), "POST");
        }

        /// <summary>Creates a market order on Binance spot.</summary>
        public Order market(string pair, float amount, Side side)
        {
            string param = $"symbol={pair}&side={side}&type=MARKET&quantity={amount}";
            return Private<Order>($"https://www.binance.com{root}order", param.Replace(",", "."), "POST");
        }
    }

    /// <summary>Binance futures class.</summary>
    public class Futures : Base
    {
        /// <summary>Default root of Binance Futures api.</summary>
        public override string root { get; set; } = "/fapi/v1/";

        /// <summary>Returns account balance of a specific coin.</summary>
        public override float balance(string coin)
        {
            return Float(account().assets.First(i => coin.Contains($"{i.asset}")).walletBalance);
        }

        /// <summary>Close all open orders of a specific coin.</summary>
        public override string close(string pair)
        {
            string param = $"symbol={pair}";
            return Private<string>($"https://www.binance.com{root}allOpenOrders", param, "DELETE");
        }

        /// <summary>Returns acccount's history of orders by pair.</summary>
        public override Order[] trades(string pair, int length)
        {
            string param = $"symbol={pair}&limit={length}";
            return Private<Order[]>($"https://www.binance.com{root}userTrades", param, "GET");
        }

        /// <summary>Creates a limit order on Binance futures.</summary>
        public Order limit(string pair, float price, float amount, int leverage, Side side)
        {
            string param = $"symbol={pair}&side={side}&type=LIMIT&timeInForce=GTC&price={price}&quantity={amount * leverage}";
            return Private<Order>($"https://www.binance.com{root}order", param.Replace(",", "."), "POST");
        }

        /// <summary>Creates a market order on Binance futures.</summary>
        public Order market(string pair, float amount, int leverage, Side side)
        {
            string param = $"symbol={pair}&side={side}&type=MARKET&quantity={amount * leverage}";
            return Private<Order>($"https://www.binance.com{root}order", param.Replace(",", "."), "POST");
        }

        /// <summary>Switch hedge mode.
        /// <para>If enable the user can trade on both short and long side at same time.</para>
        /// </summary>
        public string hedge(bool on)
        {
            string param = $"dualSidePosition={on}";
            return Private<string>($"https://www.binance.com{root}positionSide/dual", param, "POST");
        }

        /// <summary>Sets leverage size.
        /// <para>By the range of 1 - 125, but it can change depending on pair.</para>
        /// </summary>
        public string leverage(string pair, int size)
        {
            if (size < 1 || size > 125) throw new("Size must be in the range of 1 - 125.");
            string param = $"symbol={pair}&leverage={size}";
            return Private<string>($"https://www.binance.com{root}leverage", param, "POST");
        }

        /// <summary>Switch margin mode.
        /// <para>ISOLATED: Separate margin amount to each trade.</para>
        /// <para>CROSSED: Total margin amount to each trade.</para>
        /// </summary>
        public string margin(string pair, Margin mode)
        {
            string param = $"symbol={pair}&marginType={mode}";
            return Private<string>($"https://www.binance.com{root}marginType", param, "POST");
        }
    }

    /// <summary>Binance chart properties.</summary>
    public class Chart
    {
        /// <summary>Split and organize data.</summary>
        float[] getData(int index)
        {
            var range = Enumerable.Range(0, data.Length);
            var Float = (object x) => float.Parse($"{x}".Replace('.', ','));
            var split = (int i) => Float(data[i][index]);
            return range.Select(split).ToArray();
        }

        public object[][] data { get; set; }
        public float[] open => getData(1);
        public float[] high => getData(2);
        public float[] low => getData(3);
        public float[] close => getData(4);
        public float[] volume => getData(5);
        public float[] trades => getData(8);
        public float[] taker => getData(9);
    }

    /// <summary>Binance info properties.</summary>
    public class Info
    {
        /// <summary>Returns the length of decimal numbers on price.</summary>
        public int tickSize => $"{symbols[0].filters[0].tickSize}".TrimEnd('0').Split(".")[1].Length;

        /// <summary>Returns the length of decimal numbers on amount.</summary>
        public int stepSize => $"{symbols[0].filters[2].stepSize}".TrimEnd('0').Split(".")[1].Length;

        public Symbol[] symbols { get; set; }
        public class Symbol
        {
            public Filter[] filters { get; set; }
            public class Filter
            {
                /// <summary>Filters index must be [i][0].</summary>
                public object tickSize { get; set; }

                /// <summary>Filters index must be [i][2].</summary>
                public object stepSize { get; set; }
            }
        }
    }

    /// <summary>Binance account properties.</summary>
    public class Account
    {
        public Wallet[] balances { get; set; }
        public Wallet[] assets { get; set; }
        public class Wallet
        {
            public object asset { get; set; }
            public object free { get; set; }
            public object walletBalance { get; set; }
        }
    }

    /// <summary>Binance ticker properties.</summary>
    public class Ticker
    {
        public object priceChange { get; set; }
        public object priceChangePercent { get; set; }
        public object weightedAvgPrice { get; set; }
        public object prevClosePrice { get; set; }
        public object lastPrice { get; set; }
        public object lastQty { get; set; }
        public object bidPrice { get; set; }
        public object askPrice { get; set; }
        public object openPrice { get; set; }
        public object highPrice { get; set; }
        public object lowPrice { get; set; }
        public object volume { get; set; }
        public object quoteVolume { get; set; }
        public object count { get; set; }
    }

    /// <summary>Binance book properties.
    /// <para>Index of price is [i][0] and quantity is [i][1].</para>
    /// </summary>
    public class Book
    {
        public object[][] bids { get; set; }
        public object[][] asks { get; set; }
    }

    /// <summary>Binance order properties.
    /// <para>Market order returns a null price.</para>
    /// </summary>
    public class Order
    {
        public object symbol { get; set; }
        public object orderId { get; set; }
        public object price { get; set; }
        public object origQty { get; set; }
        public object status { get; set; }
        public object qty { get; set; }
        public object side { get; set; }
        public object positionSide { get; set; }
        public object isBuyerMaker { get; set; }
    }

    /// <summary>Returns timestamp for api authentication.</summary>
    struct TimeStamp
    {
        public static long Unix => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static long Ticks => DateTime.UtcNow.Ticks;
    }

    /// <summary>Hash functions for api authentication.</summary>
    struct Hash
    {
        /// <summary>Convert string to bytes.</summary>
        static byte[] e(string x, bool unicode = false)
        {
            return unicode ? Encoding.Unicode.GetBytes(x) : Encoding.UTF8.GetBytes(x);
        }

        /// <summary>SHA256 hash.</summary>
        public static string SHA256(string x, string key)
        {
            return BitConverter.ToString(new HMACSHA256(e(key)).ComputeHash(e(x))).Replace("-", "").ToLower();
        }

        /// <summary>SHA512 hash.</summary>
        public static string SHA512(string x, string key)
        {
            return BitConverter.ToString(new HMACSHA512(e(key)).ComputeHash(e(x))).Replace("-", "").ToLower();
        }
    }

    /// <summary>Rest functions</summary>
    struct REST
    {
        /// <summary>Main instance of JsonSerializerOptions.</summary>
        static readonly JsonSerializerOptions options = new() { WriteIndented = true };

        /// <summary>JSON serialize function.</summary>
        static string Serialize<T>(T x) => JsonSerializer.Serialize(x, options);

        /// <summary>JSON deserialize function.</summary>
        static T Deserialize<T>(string x)
        {
            if (typeof(T) == typeof(string))
                return (T)(object)Serialize(JsonSerializer.Deserialize<JsonElement>(x));
            else
                return JsonSerializer.Deserialize<T>(x);
        }

        /// <summary>Main instance of HttpClient.</summary>
        static readonly HttpClient http = new() { Timeout = TimeSpan.FromMilliseconds(10000) };

        /// <summary>Default REST function.</summary>
        public static T Call<T>(string url, string type = "GET", object data = null, Action<HttpRequestMessage> args = null)
        {
            using var x = new HttpRequestMessage { RequestUri = new(url), Method = new(type) };
            x.Headers.Add("User-Agent", "Chrome");
            x.Headers.Add("ContentType", "application/json");
            args?.Invoke(x);
            if (data != null)
                x.Content = new StringContent(Serialize(data));
            var response = http.SendAsync(x).Result;
            var sucess = response.IsSuccessStatusCode;
            var result = response.Content.ReadAsStringAsync().Result;
            if (!sucess) throw new(result);
            return sucess ? Deserialize<T>(result) : default;
        }
    }
}