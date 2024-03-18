﻿using Dalamud;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Lumina.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using ECommons.Reflection;

namespace visland.Helpers;

public static unsafe class Utils
{
    public static Vector4 ConvertToVector4(uint color)
    {
        var r = (byte)(color >> 24);
        var g = (byte)(color >> 16);
        var b = (byte)(color >> 8);
        var a = (byte)color;

        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
    }

    public static bool HasPlugin(string name) => DalamudReflector.TryGetDalamudPlugin(name, out var _, false, true);

    // item (button, menu item, etc.) that is disabled unless shift is held, useful for 'dangerous' operations like deletion
    public static bool DangerousItem(Func<bool> item)
    {
        bool disabled = !ImGui.IsKeyDown(ImGuiKey.ModShift);
        ImGui.BeginDisabled(disabled);
        bool res = item();
        ImGui.EndDisabled();
        if (disabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold shift");
        return res;
    }
    public static bool DangerousButton(string label) => DangerousItem(() => ImGui.Button(label));
    public static bool DangerousMenuItem(string label) => DangerousItem(() => ImGui.MenuItem(label));

    private static float startTime;
    public static void FlashText(string text, Vector4 colour1, Vector4 colour2, float duration)
    {
        float currentTime = (float)ImGui.GetTime();
        float elapsedTime = currentTime - startTime;

        float t = (float)Math.Sin(elapsedTime / duration * Math.PI * 2) * 0.5f + 0.5f;

        // Interpolate the color difference
        Vector4 interpolatedColor = new(
            colour1.X + t * (colour2.X - colour1.X),
            colour1.Y + t * (colour2.Y - colour1.Y),
            colour1.Z + t * (colour2.Z - colour1.Z),
            1.0f
        );

        ImGui.PushStyleColor(ImGuiCol.Text, interpolatedColor);
        ImGui.Text(text);
        ImGui.PopStyleColor();

        if (elapsedTime >= duration)
        {
            startTime = currentTime;
        }
    }

    // note: argument should really be any AtkEventInterface
    public static AtkValue SynthesizeEvent(AgentInterface* receiver, ulong eventKind, Span<AtkValue> args)
    {
        AtkValue res = new();
        receiver->ReceiveEvent(&res, args.GetPointer(0), (uint)args.Length, eventKind);
        return res;
    }

    // get number of owned items by id
    public static int NumItems(uint id) => InventoryManager.Instance()->GetInventoryItemCount(id);
    public static int NumCowries() => NumItems(37549);

    // sort elements of a list by key
    public static void SortBy<TValue, TKey>(this List<TValue> list, Func<TValue, TKey> proj) where TKey : notnull, IComparable => list.Sort((l, r) => proj(l).CompareTo(proj(r)));
    public static void SortByReverse<TValue, TKey>(this List<TValue> list, Func<TValue, TKey> proj) where TKey : notnull, IComparable => list.Sort((l, r) => proj(r).CompareTo(proj(l)));

    // swap two values
    public static void Swap<T>(ref T l, ref T r)
    {
        var t = l;
        l = r;
        r = t;
    }

    // get all types defined in specified assembly
    public static IEnumerable<Type?> GetAllTypes(Assembly asm)
    {
        try
        {
            return asm.DefinedTypes;
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
    }

    // get all types derived from specified type in specified assembly
    public static IEnumerable<Type> GetDerivedTypes<Base>(Assembly asm)
    {
        var b = typeof(Base);
        return GetAllTypes(asm).Where(t => t?.IsSubclassOf(b) ?? false).Select(t => t!);
    }

    public static unsafe string ToCompressedBase64<T>(T data)
    {
        try
        {
            var json = JsonConvert.SerializeObject(data, Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            using var compressedStream = new MemoryStream();
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(bytes, 0, bytes.Length);
            }

            return Convert.ToBase64String(compressedStream.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string FromCompressedBase64(this string compressedBase64)
    {
        var data = Convert.FromBase64String(compressedBase64);
        using var compressedStream = new MemoryStream(data);
        using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        zipStream.CopyTo(resultStream);
        return Encoding.UTF8.GetString(resultStream.ToArray());
    }

    public static bool EditNumberField(string labelBefore, float fieldWidth, ref int refValue, string labelAfter = "", string helpText = "")
    {
        ImGuiEx.TextV(labelBefore);

        ImGui.SameLine();

        ImGui.PushItemWidth(fieldWidth * ImGuiHelpers.GlobalScale);
        var clicked = ImGui.DragInt($"##{labelBefore}###", ref refValue);
        ImGui.PopItemWidth();

        if (labelAfter != string.Empty)
        {
            ImGui.SameLine();
            ImGuiEx.TextV(labelAfter);
        }

        if (helpText != string.Empty)
            ImGuiComponents.HelpMarker(helpText);

        return clicked;
    }
}

public static class LazyRowExtensions
{
    public static LazyRow<T> GetDifferentLanguage<T>(this LazyRow<T> row, ClientLanguage language) where T : ExcelRow
    {
        return new LazyRow<T>(Service.DataManager.GameData, row.Row, language.ToLumina());
    }
}