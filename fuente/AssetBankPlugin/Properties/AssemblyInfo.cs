using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Windows;
using AssetBankPlugin;
using Frosty.Core;
using Frosty.Core.Attributes;

[assembly: ComVisible(false)]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: Guid("4b612468-9b6a-4304-88a5-055c3575eb3d")]
[assembly: PluginDisplayName("Animation Editor")]
[assembly: PluginAuthor("Lala & Obesedwarf")]
[assembly: PluginVersion("2.0.1.0")]
[assembly: RegisterOptionsExtension(typeof(AnimationOptions), PluginManagerType.Editor)]
[assembly: RegisterAssetDefinition("AntStateAsset", typeof(AntStateAssetDefinition))]
[assembly: RegisterAssetDefinition("AntAnimationSetAsset", typeof(AntAnimationSetAssetDefinition))]
[assembly: AssemblyCompany("AssetBankPlugin")]
[assembly: AssemblyConfiguration("Developer - Debug")]
[assembly: AssemblyCopyright("Copyright ©  2020")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("BlankPlugin")]
[assembly: AssemblyTitle("BlankPlugin")]
[assembly: AssemblyVersion("1.0.0.0")]
