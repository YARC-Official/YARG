// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using System.Collections;
using System;
using System.Runtime.Serialization.Formatters.Binary;

static class Utility {
    public const int NOTFOUND = -1;
    static System.Text.StringBuilder timeFormatter = new System.Text.StringBuilder(32, 32);
    static readonly char[] numbers = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

    public static string timeConvertion(float time)
    {
        timeFormatter.Remove(0, timeFormatter.Length);
        TimeSpan timeSpan = TimeSpan.FromSeconds(time);

        if (timeSpan.Hours > 0)
        {
            return String.Format(@"{0:hh\:mm\:ss\.ff}", timeSpan);
        }
        else
        {
            return String.Format(@"{0:mm\:ss\.ff}", timeSpan);
        }
    }

    static void AppendDigit(System.Text.StringBuilder timeFormatter, int digit)
    {
        // Append the first digit
        int firstDigit = digit / 10;
        if (digit < 10)
            timeFormatter.Append('0');
        else
            timeFormatter.Append(numbers[firstDigit]);
        // Append second digit
        timeFormatter.Append(numbers[digit - firstDigit * 10]);
    }

    static int millisecondRounding(int value, int roundPlaces)
    {
        string sVal = value.ToString();

        if (sVal.Length > 0 && sVal[0] == '-')
            ++roundPlaces;

        if (sVal.Length > roundPlaces)
            sVal = sVal.Remove(roundPlaces);

        return int.Parse(sVal);
    }

    public static bool validateExtension(string filepath, string[] validExtensions)
    {
        // Need to check extension
        string extension = System.IO.Path.GetExtension(filepath);

        foreach (string validExtension in validExtensions)
        {
            if (extension == validExtension)
                return true;
        }
        return false;
    }

    public struct IntVector2
    {
        public int x, y;
        public IntVector2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
#if UNITY_EDITOR
    // https://www.codeproject.com/Articles/8102/Saving-and-obtaining-custom-objects-to-from-Window
    public static bool IsSerializable(object obj)
    {
        System.IO.MemoryStream mem = new System.IO.MemoryStream();
        BinaryFormatter bin = new BinaryFormatter();
        try
        {
            bin.Serialize(mem, obj);
            return true;
        }
        catch (Exception ex)
        {
            Debug.Log("Your object cannot be serialized." +
                             " The reason is: " + ex.ToString());
            return false;
        }
    }
#endif
}

public static class floatExtension
{
    public static float Round(this float sourceFloat, int decimalPlaces)
    {
        return (float)Math.Round(sourceFloat, decimalPlaces);
    }
}

// https://gist.github.com/darktable/2317063
public static class AudioClipExtension
{
    const int HEADER_SIZE = 44;

    public static byte[] GetWavBytes(this AudioClip clip)
    {
        byte[] chunk = GetSampleBytes(clip);
        byte[] header = GetHeader(clip, chunk.Length);
        byte[] bytes = new byte[chunk.Length + header.Length];
        Array.Copy(header, bytes, header.Length);
        Array.Copy(chunk, 0, bytes, header.Length, chunk.Length);

        return bytes;
    }

    static byte[] GetSampleBytes(AudioClip clip)
    {
        var samples = new float[clip.samples];

        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        //converting in 2 float[] steps to Int16[], //then Int16[] to Byte[]

        byte[] bytesData = new byte[samples.Length * 2];
        //bytesData array is twice the size of
        //dataSource array because a float converted in Int16 is 2 bytes.

        int rescaleFactor = 32767; //to convert float to Int16

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        return bytesData;
    }

    static byte[] GetHeader(AudioClip clip, int chunk_length)
    {
        var bytes = new System.Collections.Generic.List<byte>();
        var hz = clip.frequency;
        var channels = clip.channels;
        var samples = clip.samples;

        byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
        bytes.AddRange(riff);

        byte[] chunkSize = BitConverter.GetBytes(chunk_length - 8);
        bytes.AddRange(chunkSize);

        byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
        bytes.AddRange(wave);

        byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
        bytes.AddRange(fmt);

        byte[] subChunk1 = BitConverter.GetBytes(16);
        bytes.AddRange(subChunk1);

        const ushort AUDIO_FORMAT = 1;

        byte[] audioFormat = BitConverter.GetBytes(AUDIO_FORMAT);
        bytes.AddRange(audioFormat);

        byte[] numChannels = BitConverter.GetBytes((short)channels);
        bytes.AddRange(numChannels);

        byte[] sampleRate = BitConverter.GetBytes(hz);
        bytes.AddRange(sampleRate);

        byte[] byteRate = BitConverter.GetBytes(hz * channels * 2); // sampleRate * bytesPerSample * number of channels, here 44100*2*2
        bytes.AddRange(byteRate);

        ushort blockAlign = (ushort)(channels * 2);
        bytes.AddRange(BitConverter.GetBytes(blockAlign));

        ushort bps = 16;
        byte[] bitsPerSample = BitConverter.GetBytes(bps);
        bytes.AddRange(bitsPerSample);

        byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
        bytes.AddRange(datastring);

        byte[] subChunk2 = BitConverter.GetBytes(samples * channels * 2);
        bytes.AddRange(subChunk2);

        return bytes.ToArray();
    }
}

public static class RectTransformExtension
{
    public static Vector2 GetScreenPosition(this RectTransform source)
    {
        return RectTransformUtility.WorldToScreenPoint(null, source.transform.position);
    }

    public static Rect GetScreenCorners(this RectTransform source)
    {
        Vector3[] corners = new Vector3[4];
        Vector3[] screenCorners = new Vector3[2];

        source.GetWorldCorners(corners);

        screenCorners[0] = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        screenCorners[1] = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

        //screenCorners[0].y = Screen.height - screenCorners[0].y;
        //screenCorners[1].y = Screen.height - screenCorners[1].y;

        return new Rect(screenCorners[0], screenCorners[1] - screenCorners[0]);
    }
}

public static class Texture2DExtension
{
    public static Texture2D Inverse(this Texture2D sourceTex)
    {
        Color32[] pix = sourceTex.GetPixels32();
        System.Array.Reverse(pix);
        Texture2D destTex = new Texture2D(sourceTex.width, sourceTex.height);
        destTex.SetPixels32(pix);
        destTex.Apply();
        return destTex;
    }

    public static Texture2D HorizontalFlip(this Texture2D sourceTex)
    {
        Color32[] pix = sourceTex.GetPixels32();
        Color32[] flipped_pix = new Color32[pix.Length];

        for (int i = 0; i < pix.Length; i += sourceTex.width)
        {
            // Reverse the pixels row by row
            for (int j = i; j < i + sourceTex.width; ++j)
            {
                flipped_pix[j] = pix[i + sourceTex.width - (j - i) - 1];
            }
        }

        Texture2D destTex = new Texture2D(sourceTex.width, sourceTex.height);
        //destTex.alphaIsTransparency = sourceTex.alphaIsTransparency;
        destTex.SetPixels32(flipped_pix);
        destTex.Apply();
        return destTex;
    }

    public static Texture2D VerticalFlip(this Texture2D sourceTex)
    {
        Color32[] pix = sourceTex.GetPixels32();
        Color32[] flipped_pix = new Color32[pix.Length];

        for (int j = 0; j < sourceTex.height; ++j)
        {
            for (int i = 0; i < sourceTex.width; ++i)
            {
                flipped_pix[j * sourceTex.width + i] = pix[(sourceTex.height - 1 - j) * sourceTex.width + i];
            }
        }

        Texture2D destTex = new Texture2D(sourceTex.width, sourceTex.height);
        destTex.SetPixels32(flipped_pix);
        destTex.Apply();
        return destTex;
    }
}

public static class ColorExtension
{
    public static string GetHex(this Color col)
    {
        string hex = string.Empty;
        hex += ((int)(col.r * 255)).ToString("X2");
        hex += ((int)(col.g * 255)).ToString("X2");
        hex += ((int)(col.b * 255)).ToString("X2");
        hex += ((int)(col.a * 255)).ToString("X2");
        return hex;
    }
}
