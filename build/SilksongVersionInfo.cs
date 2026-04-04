using Nuke.Common;
using Nuke.Common.Tooling;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace _build;

[TypeConverter(typeof(Converter))]
public class SilksongVersionInfo : Enumeration
{
    public const uint STEAM_APPID = 1030300;
    public const uint STEAM_DEPOT_ID_WINDOWS = 1030301;
    public const uint STEAM_DEPOT_ID_MAC = 1030302;
    public const uint STEAM_DEPOT_ID_LINUX = 1030303;

    public static IEnumerable<SilksongVersionInfo> AllVersions => [
        .. typeof(SilksongVersionInfo).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.FieldType == typeof(SilksongVersionInfo))
            .Select(x => (SilksongVersionInfo)x.GetValue(null))
    ];
    public static IEnumerable<string> AllVersionStrings => AllVersions.Select(x => x.ToString());

    public required ulong WindowsManifestId { get; init; }
    public required ulong MacManifestId { get; init; }
    public required ulong LinuxManifestId { get; init; }

    public static readonly SilksongVersionInfo _1_0_28324 = new()
    {
        Value = "1.0.28324",
        WindowsManifestId = 3229726349000518284,
        MacManifestId = 1365730835793684614,
        LinuxManifestId = 8384590172287463475
    };

    public static readonly SilksongVersionInfo _1_0_28497 = new()
    {
        Value = "1.0.28497",
        WindowsManifestId = 539129767115354441,
        MacManifestId = 8670159430480702509,
        LinuxManifestId = 6701825740120558137
    };

    public static readonly SilksongVersionInfo _1_0_28561 = new()
    {
        Value = "1.0.28561",
        WindowsManifestId = 8642535143474926050,
        MacManifestId = 9022715293716759452,
        LinuxManifestId = 6373658714389144408
    };

    public static readonly SilksongVersionInfo _1_0_28650 = new()
    {
        Value = "1.0.28650",
        WindowsManifestId = 3900764848237536293,
        MacManifestId = 7832939953657548180,
        LinuxManifestId = 7495630131038458486
    };

    // note: win and mac have the patch for CVE-2025-59489 on 2025-10-03.
    //       linux did not get the patch and is on the release from 2025-09-24.
    //       the CVE patch contains no other changes aside from the patch.
    public static readonly SilksongVersionInfo _1_0_28714 = new()
    {
        Value = "1.0.28714",
        WindowsManifestId = 5977483240701257214,
        MacManifestId = 7917356342743942630,
        LinuxManifestId = 1617544312110692774
    };

    public static readonly SilksongVersionInfo _1_0_28891 = new()
    {
        Value = "1.0.28891",
        WindowsManifestId = 3690203822520536668,
        MacManifestId = 2374057204384257562,
        LinuxManifestId = 5954103139200615141
    };

    public static readonly SilksongVersionInfo _1_0_29242 = new()
    {
        Value = "1.0.29242",
        WindowsManifestId = 426651197780377263,
        MacManifestId = 2058007571598677908,
        LinuxManifestId = 8078874762924599313
    };

    public static readonly SilksongVersionInfo _1_0_29315 = new()
    {
        Value = "1.0.29315",
        WindowsManifestId = 3545882420322545098,
        MacManifestId = 7345001466169537628,
        LinuxManifestId = 4349246050376532986
    };

    public static readonly SilksongVersionInfo _1_0_29909 = new()
    {
        Value = "1.0.29909",
        WindowsManifestId = 3853982342899707391,
        MacManifestId = 8626896570586771768,
        LinuxManifestId = 2218100490558811317
    };

    public static readonly SilksongVersionInfo _1_0_29926 = new()
    {
        Value = "1.0.29926",
        WindowsManifestId = 3703686001292550981,
        MacManifestId = 4837712425280970616,
        LinuxManifestId = 6891565886978421564
    };

    public static readonly SilksongVersionInfo _1_0_29980 = new()
    {
        Value = "1.0.29980",
        WindowsManifestId = 468692862190470536,
        MacManifestId = 4899622998101152532,
        LinuxManifestId = 7780372375671997910
    };

    public static readonly SilksongVersionInfo _1_0_30000 = new()
    {
        Value = "1.0.30000",
        WindowsManifestId = 4421626056705534276,
        MacManifestId = 4136280015582261500,
        LinuxManifestId = 7921642076658611197
    };

    public class Converter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str)
            {
                List<SilksongVersionInfo> matches = [.. AllVersions.Where(v => v.Value == str)];
                Assert.HasSingleItem(matches);
                return matches[0];
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
