/// <summary>Simple chart indicators by adachisan.</summary>
struct Indicators
{
    /// <summary>Returns a sequence of numbers.
    /// <para>If 'start' value is bigger than 'end' value, the sequence will start backwards.</para>
    /// </summary>
    static IEnumerable<float> Range(int start, int end, Func<int, float> target)
    {
        var x = start > end ? Enumerable.Range(end, start) : Enumerable.Range(start, end);
        return start > end ? x.Reverse().Select(target).Reverse() : x.Select(target);
    }

    /// <summary>Returns a sequence of higher highs.</summary> 
    public static IEnumerable<float> Higher(float[] high, int length = 10)
    {
        if (high.Length < length) throw new("Array size needs to be greater than 'length'.");
        return Range(0, high.Length - length + 1, i => high.Skip(i).Take(length).Max());
    }

    /// <summary>Returns a sequence of lower lows.</summary> 
    public static IEnumerable<float> Lower(float[] low, int length = 10)
    {
        if (low.Length < length) throw new("Array size needs to be greater than 'length'.");
        return Range(0, low.Length - length + 1, i => low.Skip(i).Take(length).Min());
    }

    /// <summary>Returns a sequence of simple moving average.</summary> 
    public static IEnumerable<float> SMA(float[] close, int length = 10)
    {
        if (close.Length < length) throw new("Array size needs to be greater than 'length'.");
        return Range(0, close.Length - length + 1, i => close.Skip(i).Take(length).Sum() / length);
    }

    /// <summary>*Returns a sequence of weighted moving average.</summary> 
    public static IEnumerable<float> WMA(float[] close, int length = 10)
    {
        if (close.Length < length) throw new("Array size needs to be greater than 'length'.");
        for (int i = 0; i < close.Length - length + 1; i++)
        {
            float weight, weightSum = 0, sum = 0;
            for (int x = 0; x < length; x++)
            {
                weight = (length - x) * length;
                weightSum += weight;
                sum += close[i + x] * weight;
            }
            yield return sum / weightSum;
        }
    }

    /// <summary>Returns a sequence of averaged higher highs and lower lows.</summary> 
    public static IEnumerable<float> Basis(float[] high, float[] low, int length = 10)
    {
        IEnumerable<float> hst = Higher(high, length), lst = Lower(low, length);
        float calc(int i) => (hst.ElementAt(i) + lst.ElementAt(i)) / 2;
        return Range(0, high.Length - length + 1, calc);
    }

    /// <summary>Returns a sequence of stochastic indicator.</summary> 
    public static IEnumerable<float> Stoch(float[] close, float[] high, float[] low, int length = 10)
    {
        IEnumerable<float> hst = Higher(high, length), lst = Lower(low, length);
        float calc(int i) => (close[i] - lst.ElementAt(i)) / (hst.ElementAt(i) - lst.ElementAt(i)) * 100.0f;
        return Range(0, close.Length - length + 1, calc);
    }

    /// <summary>Returns a sequence of stochastic bollinger bands.</summary> 
    public static IEnumerable<float> Bands(float[] close, int length = 10, float mult = 2)
    {
        IEnumerable<float> sma = SMA(close, length);
        return Range(0, close.Length - length + 1, i =>
         {
             float basis = sma.ElementAt(i);
             float calc(float x) => (float)Math.Pow(x - basis, 2) / length;
             float dev = (float)Math.Sqrt(close.Skip(i).Take(length).Select(calc).Sum());
             float upper = basis + (mult * dev), lower = basis - (mult * dev);
             return (close[i] - lower) / (upper - lower) * 100.0f;
         });
    }

    /// <summary>Returns a sequence of exponential moving average.</summary> 
    public static IEnumerable<float> EMA(float[] close, int length = 10)
    {
        if (close.Length < length * 3) throw new("Array size needs to be greater than 'length' * 3.");
        float sma = SMA(close, length).Last(), mult = 2.0f / (length + 1.0f), value = 0.0f;
        float calc(int i) => value = i == close.Length - length ? sma : mult * close[i] + (1.0f - mult) * value;
        return Range(close.Length - length + 1, 0, calc);
    }

    /// <summary>Returns a sequence of running moving average.</summary> 
    public static IEnumerable<float> RMA(float[] close, int length = 10)
    {
        if (close.Length < length * 3) throw new("Array size needs to be greater than 'length' * 3.");
        float sma = SMA(close, length).Last(), mult = 1.0f / length, value = 0.0f;
        float calc(int i) => value = i == close.Length - length ? sma : mult * close[i] + (1.0f - mult) * value;
        return Range(close.Length - length + 1, 0, calc);
    }

    /// <summary>Returns a sequence of relative strength index.</summary> 
    public static IEnumerable<float> RSI(float[] close, int length = 14)
    {
        if (close.Length < 2) throw new("Array size needs to be greater than 1.");
        var up = Range(close.Length - 1, 0, i => Math.Max(0, close[i] - close[i + 1])).ToArray();
        var dn = Range(close.Length - 1, 0, i => Math.Max(0, close[i + 1] - close[i])).ToArray();
        return RMA(up, length).Zip(RMA(dn, length), (a, b) => a / b).Select(i => 100.0f - 100.0f / (1.0f + i));
    }

    /// <summary>Returns a sequence of true range.</summary> 
    public static IEnumerable<float> TR(float[] close, float[] high, float[] low)
    {
        if (close.Length < 2) throw new("Array size needs to be greater than 1.");
        float max(float a, float b, float c) => new float[] { a, Math.Abs(b), Math.Abs(c) }.Max();
        return Range(0, close.Length - 1, i => max(high[i] - low[i], high[i] - close[i + 1], low[i] - close[i + 1]));
    }

    /// <summary>Returns a sequence of averaged true range.</summary> 
    public static IEnumerable<float> ATR(float[] close, float[] high, float[] low, int length = 10)
    {
        return RMA(TR(close, high, low).ToArray(), length);
    }

    /// <summary>*Returns a sequence of supertrend indicator.</summary> 
    public static IEnumerable<float> Super(float[] close, float[] high, float[] low, int length = 10, float mult = 3)
    {
        float[] atr = ATR(close, high, low, length).ToArray(), sp = new float[atr.Length];
        float[] upline = new float[atr.Length], dnline = new float[atr.Length];
        return Range(atr.Length - 1, 0, i =>
        {
            float up = Math.Max(0, ((high[i] + low[i]) / 2) + (mult * atr[i]));
            float dn = Math.Max(0, ((high[i] + low[i]) / 2) - (mult * atr[i]));

            upline[i] = up < upline[i + 1] || close[i + 1] > upline[i + 1] ? up : upline[i + 1];
            dnline[i] = dn > dnline[i + 1] || close[i + 1] < dnline[i + 1] ? dn : dnline[i + 1];

            return sp[i] = sp[i + 1] == upline[i + 1] && close[i] <= upline[i] ? upline[i] :
            sp[i + 1] == upline[i + 1] && close[i] >= upline[i] ? dnline[i] :
            sp[i + 1] == dnline[i + 1] && close[i] >= dnline[i] ? dnline[i] :
            sp[i + 1] == dnline[i + 1] && close[i] <= dnline[i] ? upline[i] : 0;
        });
    }
}