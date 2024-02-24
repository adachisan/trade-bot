using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using Binance;
using static Trade.Functions;

//dotnet publish -c Release -r win10-x64 --self-contained false /p:PublishSingleFile=true /p:PublishTrimmed=false /p:PublishReadyToRun=false
namespace Trade;

class Program
{
    static readonly Spot spot = new Spot();
    static Data data = new Data();
    static bool fill = false;
    static int length => data.pairs.Count;

    static void Main(string[] args)
    {
        if (File.Exists("data.json"))
            data = JsonSerializer.Deserialize<Data>(File.ReadAllText("data.json"));
        spot.login = (data.key, data.secret);
        Clear();

        var ParallelOpt = new ParallelOptions { MaxDegreeOfParallelism = 4 };
        Loop(() =>
        {
            Trade[] tradeList = new Trade[length];
            Order[] openList = data.key != "" ? spot.orders() : new Order[0];

            Parallel.For(0, length, ParallelOpt, i =>
            {
                bool isOpen = openList.Any(x => $"{x.symbol}" == data.pairs[i]);
                Chart candles = spot.chart(data.pairs[i], "1d", 50);
                if (candles != null)
                {
                    float rsi = Indicators.RSI(candles.close, 14).First();
                    float sma = candles.close[0] / Indicators.SMA(candles.close, 30).First() * 100 - 100;
                    tradeList[i] = new Trade { pair = data.pairs[i], locked = isOpen, price = candles.close[0], rsi = rsi, sma = sma };
                }
            });

            Array.Sort(tradeList, (a, b) => a.sma.CompareTo(b.sma));
            string[] buyList = Array.Empty<string>();

            var isLocked = (Trade i) => i.locked ? i.pair : null;
            var isNotLocked = (Trade i) => !i.locked ? i.pair : null;

            var lockedList = tradeList.Select(isLocked).Where(IsNotNull).ToArray();
            if (IsNotNull(openList) && lockedList.Length < data.trades)
            {
                int max = data.trades - lockedList.Length;
                buyList = tradeList.Select(isNotLocked).Where(IsNotNull).Take(max).ToArray();
                if (fill) { Buy(buyList); fill = false; }
            }

            string[,] menuItems = new string[length + 1, 4];
            ConsoleColor[] colors = new ConsoleColor[length + 1];
            for (int i = 0; i < length + 1 && Menu.Visible; i++)
            {
                if (i == 0)
                {
                    menuItems[i, 0] = "PAIR";
                    menuItems[i, 1] = "PRICE";
                    menuItems[i, 2] = "RSI";
                    menuItems[i, 3] = "SMA";
                    colors[i] = ConsoleColor.Yellow;
                }
                else
                {
                    Trade item = tradeList[i - 1];
                    menuItems[i, 0] = $"{item.pair}";
                    menuItems[i, 1] = $"{item.price:n4}";
                    menuItems[i, 2] = $"{item.rsi:n2}";
                    menuItems[i, 3] = $"{item.sma:n2}";
                    var anyPair = (string x) => x == item.pair;
                    if (lockedList.Any(anyPair)) colors[i] = ConsoleColor.Red;
                    if (buyList.Any(anyPair)) colors[i] = ConsoleColor.Green;
                }
            }
            Menu.Table(menuItems, 7, colors);
            if (!fill) RefreshTime = DateTime.Now.AddSeconds(30);
        });

        while (true)
        {
            Menu.Title = $"Gain: {data.gain:n2}%  Dip: {data.dip:n2}%  Bet: ${data.bet:n2}  Trades: {data.trades}";
            string[] x = Menu.Read(length + 2).Split(' ');
            var commands = new Dictionary<string, Action>
            {
                { "hide", () => Menu.Visible = false },
                { "clear", () => Clear() },
                { "fill", () => fill = true },
                { "close", () => Environment.Exit(0) },
                { "key", () => { data.key = x[1]; spot.login = (x[1], spot.login.secret); } },
                { "secret", () => { data.secret = x[1]; spot.login = (spot.login.key, x[1]); } },
                { "gain", () => { var n = x[1].ToFloat(); data.gain = n < 1 ? 1 : n; } },
                { "dip", () => { var n = x[1].ToFloat(); data.dip = n < 10 ? 10 : n; } },
                { "bet", () => { var n = x[1].ToFloat(); data.bet = n < 10 ? 10 : n; } },
                { "trades", () => { var n = int.Parse(x[1]); data.trades = n < 1 ? 1 : n; } },
                { "add", () => Add(x[1].ToUpper()) },
                { "del", () => Del(x[1].ToUpper()) },
                { "buy", () => Buy(x[1].ToUpper()) },
                { "wall", () => Wall(x[1].ToUpper(), x[2].ToFloat()) }
            };
            if (commands.ContainsKey(x[0].ToLower())) { commands[x[0].ToLower()](); Save(); }
        }
    }

    static void Clear()
    {
        Menu.Clear();
        Menu.Size = (60, length + 4);
        RefreshTime = DateTime.Now;
    }

    static void Buy(string[] pairs)
    {
        foreach (var i in pairs) Buy(i);
    }

    static void Buy(string pair)
    {
        try
        {
            Chart candle = spot.chart(pair, "1d", 1);
            if (IsNull(candle) || !data.pairs.Any(i => i == pair)) return;
            float amount = (data.bet / candle.close[0]).ToFixed(spot.info(pair).stepSize);
            Log($"BUY-MKT {candle.close[0]:n4} {amount:n4} {data.bet:n2}");
            Order market = spot.market(pair, amount, Binance.Side.BUY);
            if (IsNotNull(market)) Wall(pair, candle.close[0]);
        }
        catch (Exception e) { Log(e.Message); }
    }

    static void Wall(string pair, float price)
    {
        try
        {
            Info info = spot.info(pair);
            int tick = IsNotNull(info) ? info.tickSize : 2;
            int step = IsNotNull(info) ? info.stepSize : 2;
            Chart candle = spot.chart(pair, "1d", 1);
            if (IsNull(candle) || !data.pairs.Any(i => i == pair)) return;
            float sellPrice = candle.close[0] * (1 + data.gain * 0.01f);
            float sellAmount = (data.bet / sellPrice).ToFixed(step);
            Log($"SEL-LMT {sellPrice:n4} {sellAmount:n4} {data.bet:n2}");
            spot.limit(pair, sellPrice.ToFixed(tick), sellAmount, Binance.Side.SELL);
            for (int i = 1; i <= (80 / data.dip).ToFixed(0); i++)
            {
                float buyPrice = price * (1 - data.dip * 0.01f * i);
                float buyAmount = (data.bet / buyPrice).ToFixed(step);
                Log($"BUY-LMT {buyPrice:n4} {buyAmount:n4} {data.bet:n2}");
                spot.limit(pair, buyPrice.ToFixed(tick), buyAmount, Binance.Side.BUY);
            }
        }
        catch (Exception e) { Log(e.Message); }
    }

    class Trade
    {
        public string pair { get; set; } = "";
        public bool locked { get; set; } = false;
        public float price { get; set; } = 0;
        public float rsi { get; set; } = 0;
        public float sma { get; set; } = 0;
    }

    class Data
    {
        public string key { get; set; } = "";
        public string secret { get; set; } = "";
        public float gain { get; set; } = 10;
        public float dip { get; set; } = 25;
        public float bet { get; set; } = 11;
        public int trades { get; set; } = 7;
        public List<string> pairs { get; set; } = new List<string>
        {
            "BTCBUSD", "ETHBUSD", "BNBBUSD", "ADABUSD", "DOTBUSD", "CAKEBUSD", "UNIBUSD", "LINKBUSD",
            "AXSBUSD", "SOLBUSD", "MATICBUSD", "AVAXBUSD", "LUNABUSD", "ATOMBUSD", "TRXBUSD", "ALGOBUSD"
        };
    }

    static void Save()
    {
        Ignore(() => File.WriteAllText("data.json", JsonSerializer.Serialize(data)));
    }

    static void Add(string pair)
    {
        if (!data.pairs.Any(i => i == pair) && IsNotNull(spot.info(pair)))
            data.pairs.Add(pair);
    }

    static void Del(string pair)
    {
        if (data.pairs.Any(i => i == pair)) data.pairs.Remove(pair);
    }
}

static class Functions
{
    /// <summary>Returns true if null.</summary>
    public static bool IsNull<T>(T x) => x == null;

    /// <summary>Returns true if not null.</summary>
    public static bool IsNotNull<T>(T x) => x != null;

    /// <summary>Sets max decimal places without rounding it.</summary>
    public static float ToFixed(this float x, int places)
    {
        return (float)(Math.Truncate((double)x * Math.Pow(10, places)) / Math.Pow(10, places));
    }

    /// <summary>Converts any type of objeto to float.</summary>
    public static float ToFloat<T>(this T x) => float.Parse($"{x}".Replace('.', ','));

    /// <summary>Ignore errors of a specific action.</summary>
    public static void Ignore(Action action)
    {
        try { action.Invoke(); }
        catch (Exception e) { Debug.WriteLine(e.Message); }
    }

    /// <summary>Log text on file.</summary>
    public static void Log(string text)
    {
        Debug.WriteLine($"{DateTime.Now:T}  {text}");
        string today = $"{DateTime.Now:d}".Replace("/", "-");
        if (!Directory.Exists("log")) Directory.CreateDirectory("log");
        Ignore(() => File.AppendAllText($"log/{today}.txt", $"{DateTime.Now:T}  {text}\n"));
    }

    /// <summary>Store refresh time of loop function.</summary>
    public static DateTime RefreshTime = DateTime.Now;

    /// <summary>Loop function that runs in another thread.</summary>
    public static Task Loop(Action action)
    {
        return Task.Run(() =>
        {
            while (true)
            {
                try { if (DateTime.Now >= RefreshTime) action(); }
                catch (Exception e) { Log(e.Message); }
                Thread.Sleep(1000);
            }
        });
    }
}