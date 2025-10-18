using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RichTextParser
{
    public class ShakeRange
    {
        public int start;
        public int length;
        public float intensity;
    }

    public class WaveRange
    {
        public int start;
        public int length;
        public float amplitude;
        public float speed;
    }

    public class PulseRange
    {
        public int start;
        public int length;
        public float scale;
        public float speed;
    }

    public class GradientRange
    {
        public int start;
        public int length;
        public Color startColor;
        public Color endColor;
        public bool rainbow;
    }

    // Parses custom square-bracket tags into TMP tags and returns shake ranges (character offsets in the final output)
    public static void Parse(string input, out string output, out List<ShakeRange> shakes,
        out List<WaveRange> waves, out List<PulseRange> pulses, out List<GradientRange> gradients)
    {
        var sb = new StringBuilder();
        shakes = new List<ShakeRange>();
        waves = new List<WaveRange>();
        pulses = new List<PulseRange>();
        gradients = new List<GradientRange>();

        var shakeStack = new Stack<(int startVisibleIndex, float intensity)>();
        var waveStack = new Stack<(int startVisibleIndex, float amplitude, float speed)>();
        var pulseStack = new Stack<(int startVisibleIndex, float scale, float speed)>();
        var gradientStack = new Stack<(int startVisibleIndex, Color startColor, Color endColor, bool rainbow)>();
        int visibleIndex = 0; // counts only visible characters (ignores tag chars)

        for (int i = 0; i < input.Length; )
        {
            if (input[i] == '[')
            {
                int end = input.IndexOf(']', i);
                if (end == -1)
                {
                    // no closing bracket, copy rest as visible text
                    var rest = input.Substring(i);
                    sb.Append(rest);
                    visibleIndex += rest.Length;
                    break;
                }

                string tag = input.Substring(i + 1, end - i - 1).Trim();
                i = end + 1;

                // Closing tags
                if (tag.StartsWith("/"))
                {
                    string name = tag.Substring(1);
                    if (name.Equals("b", StringComparison.OrdinalIgnoreCase)) sb.Append("</b>");
                    else if (name.Equals("i", StringComparison.OrdinalIgnoreCase)) sb.Append("</i>");
                    else if (name.Equals("u", StringComparison.OrdinalIgnoreCase)) sb.Append("</u>");
                    else if (name.Equals("color", StringComparison.OrdinalIgnoreCase)) sb.Append("</color>");
                    else if (name.Equals("shake", StringComparison.OrdinalIgnoreCase))
                    {
                        if (shakeStack.Count > 0)
                        {
                            var s = shakeStack.Pop();
                            var range = new ShakeRange { start = s.startVisibleIndex, length = visibleIndex - s.startVisibleIndex, intensity = s.intensity };
                            if (range.length > 0) shakes.Add(range);
                        }
                    }
                    else if (name.Equals("wave", StringComparison.OrdinalIgnoreCase))
                    {
                        if (waveStack.Count > 0)
                        {
                            var w = waveStack.Pop();
                            var range = new WaveRange { start = w.startVisibleIndex, length = visibleIndex - w.startVisibleIndex, amplitude = w.amplitude, speed = w.speed };
                            if (range.length > 0) waves.Add(range);
                        }
                    }
                    else if (name.Equals("pulse", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pulseStack.Count > 0)
                        {
                            var p = pulseStack.Pop();
                            var range = new PulseRange { start = p.startVisibleIndex, length = visibleIndex - p.startVisibleIndex, scale = p.scale, speed = p.speed };
                            if (range.length > 0) pulses.Add(range);
                        }
                    }
                    else if (name.Equals("gradient", StringComparison.OrdinalIgnoreCase))
                    {
                        if (gradientStack.Count > 0)
                        {
                            var g = gradientStack.Pop();
                            var range = new GradientRange { start = g.startVisibleIndex, length = visibleIndex - g.startVisibleIndex, startColor = g.startColor, endColor = g.endColor, rainbow = g.rainbow };
                            if (range.length > 0) gradients.Add(range);
                        }
                    }
                    else
                    {
                        // unknown close tag -> ignore
                    }

                    continue;
                }

                // Opening tags or single tags
                if (tag.Equals("b", StringComparison.OrdinalIgnoreCase)) { sb.Append("<b>"); continue; }
                if (tag.Equals("i", StringComparison.OrdinalIgnoreCase)) { sb.Append("<i>"); continue; }
                if (tag.Equals("u", StringComparison.OrdinalIgnoreCase)) { sb.Append("<u>"); continue; }

                if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                {
                    string col = tag.Substring(6);
                    sb.Append($"<color={col}>");
                    continue;
                }

                if (tag.StartsWith("shake", StringComparison.OrdinalIgnoreCase))
                {
                    float intensity = 1f;
                    int eq = tag.IndexOf('=');
                    if (eq >= 0)
                    {
                        float.TryParse(tag.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out intensity);
                    }
                    // push start based on visible character count
                    shakeStack.Push((visibleIndex, intensity));
                    continue;
                }

                if (tag.StartsWith("wave", StringComparison.OrdinalIgnoreCase))
                {
                    // syntax: wave=amplitude,speed  (both optional)
                    float amplitude = 5f;
                    float speed = 3f;
                    int eq = tag.IndexOf('=');
                    if (eq >= 0)
                    {
                        var args = tag.Substring(eq + 1).Split(',');
                        if (args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out amplitude);
                        if (args.Length > 1) float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed);
                    }
                    waveStack.Push((visibleIndex, amplitude, speed));
                    continue;
                }

                if (tag.StartsWith("pulse", StringComparison.OrdinalIgnoreCase))
                {
                    // syntax: pulse=scale,speed
                    float scale = 1.2f;
                    float speed = 3f;
                    int eq = tag.IndexOf('=');
                    if (eq >= 0)
                    {
                        var args = tag.Substring(eq + 1).Split(',');
                        if (args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale);
                        if (args.Length > 1) float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed);
                    }
                    pulseStack.Push((visibleIndex, scale, speed));
                    continue;
                }

                if (tag.StartsWith("gradient", StringComparison.OrdinalIgnoreCase))
                {
                    // syntax: gradient=#RRGGBB,#RRGGBB or gradient=rainbow
                    Color c1 = Color.white;
                    Color c2 = Color.white;
                    bool rainbow = false;
                    int eq = tag.IndexOf('=');
                    if (eq >= 0)
                    {
                        var arg = tag.Substring(eq + 1);
                        if (arg.Equals("rainbow", StringComparison.OrdinalIgnoreCase))
                        {
                            rainbow = true;
                        }
                        else
                        {
                            var parts = arg.Split(',');
                            if (parts.Length > 0) ColorUtility.TryParseHtmlString(parts[0].Trim(), out c1);
                            if (parts.Length > 1) ColorUtility.TryParseHtmlString(parts[1].Trim(), out c2);
                        }
                    }
                    gradientStack.Push((visibleIndex, c1, c2, rainbow));
                    continue;
                }

                // Unknown opening tag: copy literally (for forgiving UX)
                sb.Append('[').Append(tag).Append(']');
                visibleIndex += tag.Length + 2;
                continue;
            }

            // normal visible char
            sb.Append(input[i]);
            visibleIndex++;
            i++;
        }

        // Close any unclosed shake tags
        while (shakeStack.Count > 0)
        {
            var s = shakeStack.Pop();
            var range = new ShakeRange { start = s.startVisibleIndex, length = visibleIndex - s.startVisibleIndex, intensity = s.intensity };
            if (range.length > 0) shakes.Add(range);
        }

        // Close any unclosed wave tags
        while (waveStack.Count > 0)
        {
            var w = waveStack.Pop();
            var range = new WaveRange { start = w.startVisibleIndex, length = visibleIndex - w.startVisibleIndex, amplitude = w.amplitude, speed = w.speed };
            if (range.length > 0) waves.Add(range);
        }

        // Close any unclosed pulse tags
        while (pulseStack.Count > 0)
        {
            var p = pulseStack.Pop();
            var range = new PulseRange { start = p.startVisibleIndex, length = visibleIndex - p.startVisibleIndex, scale = p.scale, speed = p.speed };
            if (range.length > 0) pulses.Add(range);
        }

        // Close any unclosed gradient tags
        while (gradientStack.Count > 0)
        {
            var g = gradientStack.Pop();
            var range = new GradientRange { start = g.startVisibleIndex, length = visibleIndex - g.startVisibleIndex, startColor = g.startColor, endColor = g.endColor, rainbow = g.rainbow };
            if (range.length > 0) gradients.Add(range);
        }

        output = sb.ToString();
    }
}