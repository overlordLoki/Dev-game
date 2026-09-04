; ModuleID = 'marshal_methods.arm64-v8a.ll'
source_filename = "marshal_methods.arm64-v8a.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

%struct.MarshalMethodName = type {
	i64, ; uint64_t id
	ptr ; char* name
}

%struct.MarshalMethodsManagedClass = type {
	i32, ; uint32_t token
	ptr ; MonoClass klass
}

@assembly_image_cache = dso_local local_unnamed_addr global [183 x ptr] zeroinitializer, align 8

; Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array
@assembly_image_cache_hashes = dso_local local_unnamed_addr constant [549 x i64] [
	i64 u0x001e58127c546039, ; 0: lib_System.Globalization.dll.so => 44
	i64 u0x01109b0e4d99e61f, ; 1: System.ComponentModel.Annotations.dll => 15
	i64 u0x0284512fad379f7e, ; 2: System.Runtime.Handles => 109
	i64 u0x02abedc11addc1ed, ; 3: lib_Mono.Android.Runtime.dll.so => 176
	i64 u0x02f55bf70672f5c8, ; 4: lib_System.IO.FileSystem.DriveInfo.dll.so => 50
	i64 u0x03621c804933a890, ; 5: System.Buffers => 9
	i64 u0x0399610510a38a38, ; 6: lib_System.Private.DataContractSerialization.dll.so => 90
	i64 u0x0517ef04e06e9f76, ; 7: System.Net.Primitives => 74
	i64 u0x0581db89237110e9, ; 8: lib_System.Collections.dll.so => 14
	i64 u0x05a1c25e78e22d87, ; 9: lib_System.Runtime.CompilerServices.Unsafe.dll.so => 106
	i64 u0x06388ffe9f6c161a, ; 10: System.Xml.Linq.dll => 161
	i64 u0x06600c4c124cb358, ; 11: System.Configuration.dll => 21
	i64 u0x069fff96ec92a91d, ; 12: System.Xml.XPath.dll => 166
	i64 u0x07469f2eecce9e85, ; 13: mscorlib.dll => 172
	i64 u0x07dcdc7460a0c5e4, ; 14: System.Collections.NonGeneric => 12
	i64 u0x08a7c865576bbde7, ; 15: System.Reflection.Primitives => 100
	i64 u0x09138715c92dba90, ; 16: lib_System.ComponentModel.Annotations.dll.so => 15
	i64 u0x092266563089ae3e, ; 17: lib_System.Collections.NonGeneric.dll.so => 12
	i64 u0x09d144a7e214d457, ; 18: System.Security.Cryptography => 131
	i64 u0x09e2b9f743db21a8, ; 19: lib_System.Reflection.Metadata.dll.so => 99
	i64 u0x0abb3e2b271edc45, ; 20: System.Threading.Channels.dll => 145
	i64 u0x0b06b1feab070143, ; 21: System.Formats.Tar => 41
	i64 u0x0c59ad9fbbd43abe, ; 22: Mono.Android => 177
	i64 u0x0c74af560004e816, ; 23: Microsoft.Win32.Registry.dll => 7
	i64 u0x0c83c82812e96127, ; 24: lib_System.Net.Mail.dll.so => 70
	i64 u0x0d13cd7cce4284e4, ; 25: System.Security.SecureString => 134
	i64 u0x0e14e73a54dda68e, ; 26: lib_System.Net.NameResolution.dll.so => 71
	i64 u0x0f5e7abaa7cf470a, ; 27: System.Net.HttpListener => 69
	i64 u0x1001f97bbe242e64, ; 28: System.IO.UnmanagedMemoryStream => 59
	i64 u0x1065c4cb554c3d75, ; 29: System.IO.IsolatedStorage.dll => 54
	i64 u0x10f6cfcbcf801616, ; 30: System.IO.Compression.Brotli => 45
	i64 u0x114443cdcf2091f1, ; 31: System.Security.Cryptography.Primitives => 129
	i64 u0x11a603952763e1d4, ; 32: System.Net.Mail => 70
	i64 u0x11a70d0e1009fb11, ; 33: System.Net.WebSockets.dll => 85
	i64 u0x12128b3f59302d47, ; 34: lib_System.Xml.Serialization.dll.so => 163
	i64 u0x123639456fb056da, ; 35: System.Reflection.Emit.Lightweight.dll => 96
	i64 u0x12521e9764603eaa, ; 36: lib_System.Resources.Reader.dll.so => 103
	i64 u0x12d3b63863d4ab0b, ; 37: lib_System.Threading.Overlapped.dll.so => 146
	i64 u0x134eab1061c395ee, ; 38: System.Transactions => 156
	i64 u0x13beedefb0e28a45, ; 39: lib_System.Xml.XmlDocument.dll.so => 167
	i64 u0x13f1e5e209e91af4, ; 40: lib_Java.Interop.dll.so => 174
	i64 u0x1497051b917530bd, ; 41: lib_System.Net.WebSockets.dll.so => 85
	i64 u0x152a448bd1e745a7, ; 42: Microsoft.Win32.Primitives => 6
	i64 u0x1557de0138c445f4, ; 43: lib_Microsoft.Win32.Registry.dll.so => 7
	i64 u0x15bdc156ed462f2f, ; 44: lib_System.IO.FileSystem.dll.so => 53
	i64 u0x15e300c2c1668655, ; 45: System.Resources.Writer.dll => 105
	i64 u0x16bf2a22df043a09, ; 46: System.IO.Pipes.dll => 58
	i64 u0x16ea2b318ad2d830, ; 47: System.Security.Cryptography.Algorithms => 124
	i64 u0x16eeae54c7ebcc08, ; 48: System.Reflection.dll => 102
	i64 u0x17125c9a85b4929f, ; 49: lib_netstandard.dll.so => 173
	i64 u0x1716866f7416792e, ; 50: lib_System.Security.AccessControl.dll.so => 122
	i64 u0x1752c12f1e1fc00c, ; 51: System.Core => 23
	i64 u0x17f9358913beb16a, ; 52: System.Text.Encodings.Web => 141
	i64 u0x1809fb23f29ba44a, ; 53: lib_System.Reflection.TypeExtensions.dll.so => 101
	i64 u0x18a9befae51bb361, ; 54: System.Net.WebClient => 81
	i64 u0x1991f3d52a00940a, ; 55: lib_merder.dll.so => 2
	i64 u0x19a4c090f14ebb66, ; 56: System.Security.Claims => 123
	i64 u0x1a91866a319e9259, ; 57: lib_System.Collections.Concurrent.dll.so => 10
	i64 u0x1aac34d1917ba5d3, ; 58: lib_System.dll.so => 170
	i64 u0x1aea8f1c3b282172, ; 59: lib_System.Net.Ping.dll.so => 73
	i64 u0x1c753b5ff15bce1b, ; 60: Mono.Android.Runtime.dll => 176
	i64 u0x1cd47467799d8250, ; 61: System.Threading.Tasks.dll => 150
	i64 u0x1d23eafdc6dc346c, ; 62: System.Globalization.Calendars.dll => 42
	i64 u0x1db6820994506bf5, ; 63: System.IO.FileSystem.AccessControl.dll => 49
	i64 u0x1dbb0c2c6a999acb, ; 64: System.Diagnostics.StackTrace => 32
	i64 u0x1e7c31185e2fb266, ; 65: lib_System.Threading.Tasks.Parallel.dll.so => 149
	i64 u0x1f055d15d807e1b2, ; 66: System.Xml.XmlSerializer => 168
	i64 u0x1f1ed22c1085f044, ; 67: lib_System.Diagnostics.FileVersionInfo.dll.so => 30
	i64 u0x1f61df9c5b94d2c1, ; 68: lib_System.Numerics.dll.so => 88
	i64 u0x20237ea48006d7a8, ; 69: lib_System.Net.WebClient.dll.so => 81
	i64 u0x209375905fcc1bad, ; 70: lib_System.IO.Compression.Brotli.dll.so => 45
	i64 u0x20b4613ede4130ec, ; 71: fr-FR/merder.Core.resources.dll => 1
	i64 u0x20fab3cf2dfbc8df, ; 72: lib_System.Diagnostics.Process.dll.so => 31
	i64 u0x2110167c128cba15, ; 73: System.Globalization => 44
	i64 u0x21419508838f7547, ; 74: System.Runtime.CompilerServices.VisualC => 107
	i64 u0x2174319c0d835bc9, ; 75: System.Runtime => 121
	i64 u0x219ea1b751a4dee4, ; 76: lib_System.IO.Compression.ZipFile.dll.so => 47
	i64 u0x21cc7e445dcd5469, ; 77: System.Reflection.Emit.ILGeneration => 95
	i64 u0x224538d85ed15a82, ; 78: System.IO.Pipes => 58
	i64 u0x22908438c6bed1af, ; 79: lib_System.Threading.Timer.dll.so => 153
	i64 u0x237be844f1f812c7, ; 80: System.Threading.Thread.dll => 151
	i64 u0x23852b3bdc9f7096, ; 81: System.Resources.ResourceManager => 104
	i64 u0x23986dd7e5d4fc01, ; 82: System.IO.FileSystem.Primitives.dll => 51
	i64 u0x2407aef2bbe8fadf, ; 83: System.Console => 22
	i64 u0x247619fe4413f8bf, ; 84: System.Runtime.Serialization.Primitives.dll => 118
	i64 u0x26a670e154a9c54b, ; 85: System.Reflection.Extensions.dll => 98
	i64 u0x26d077d9678fe34f, ; 86: System.IO.dll => 60
	i64 u0x2759af78ab94d39b, ; 87: System.Net.WebSockets => 85
	i64 u0x27b410442fad6cf1, ; 88: Java.Interop.dll => 174
	i64 u0x27b97e0d52c3034a, ; 89: System.Diagnostics.Debug => 28
	i64 u0x2801845a2c71fbfb, ; 90: System.Net.Primitives.dll => 74
	i64 u0x2a3b095612184159, ; 91: lib_System.Net.NetworkInformation.dll.so => 72
	i64 u0x2a6507a5ffabdf28, ; 92: System.Diagnostics.TraceSource.dll => 35
	i64 u0x2ad5d6b13b7a3e04, ; 93: System.ComponentModel.DataAnnotations.dll => 16
	i64 u0x2af298f63581d886, ; 94: System.Text.RegularExpressions.dll => 143
	i64 u0x2afc1c4f898552ee, ; 95: lib_System.Formats.Asn1.dll.so => 40
	i64 u0x2cbd9262ca785540, ; 96: lib_System.Text.Encoding.CodePages.dll.so => 138
	i64 u0x2cc9e1fed6257257, ; 97: lib_System.Reflection.Emit.Lightweight.dll.so => 96
	i64 u0x2cd723e9fe623c7c, ; 98: lib_System.Private.Xml.Linq.dll.so => 92
	i64 u0x2d169d318a968379, ; 99: System.Threading.dll => 154
	i64 u0x2d5ffcae1ad0aaca, ; 100: System.Data.dll => 26
	i64 u0x2db915caf23548d2, ; 101: System.Text.Json.dll => 142
	i64 u0x2dcaa0bb15a4117a, ; 102: System.IO.UnmanagedMemoryStream.dll => 59
	i64 u0x2e2ced2c3c6a1edc, ; 103: lib_System.Threading.AccessControl.dll.so => 144
	i64 u0x2e5a40c319acb800, ; 104: System.IO.FileSystem => 53
	i64 u0x2f02f94df3200fe5, ; 105: System.Diagnostics.Process => 31
	i64 u0x2f2e98e1c89b1aff, ; 106: System.Xml.ReaderWriter => 162
	i64 u0x2f5911d9ba814e4e, ; 107: System.Diagnostics.Tracing => 36
	i64 u0x2f84070a459bc31f, ; 108: lib_System.Xml.dll.so => 169
	i64 u0x30c6dda129408828, ; 109: System.IO.IsolatedStorage => 54
	i64 u0x31195fef5d8fb552, ; 110: _Microsoft.Android.Resource.Designer.dll => 182
	i64 u0x31496b779ed0663d, ; 111: lib_System.Reflection.DispatchProxy.dll.so => 94
	i64 u0x3235427f8d12dae1, ; 112: lib_System.Drawing.Primitives.dll.so => 37
	i64 u0x32aa989ff07a84ff, ; 113: lib_System.Xml.ReaderWriter.dll.so => 162
	i64 u0x33829542f112d59b, ; 114: System.Collections.Immutable => 11
	i64 u0x341abc357fbb4ebf, ; 115: lib_System.Net.Sockets.dll.so => 80
	i64 u0x346a212343615ac5, ; 116: lib_System.Linq.AsyncEnumerable.dll.so => 61
	i64 u0x3496c1e2dcaf5ecc, ; 117: lib_System.IO.Pipes.AccessControl.dll.so => 57
	i64 u0x353590da528c9d22, ; 118: System.ComponentModel.Annotations => 15
	i64 u0x355c649948d55d97, ; 119: lib_System.Runtime.Intrinsics.dll.so => 113
	i64 u0x3628ab68db23a01a, ; 120: lib_System.Diagnostics.Tools.dll.so => 34
	i64 u0x3673b042508f5b6b, ; 121: lib_System.Runtime.Extensions.dll.so => 108
	i64 u0x36740f1a8ecdc6c4, ; 122: System.Numerics => 88
	i64 u0x36b2b50fdf589ae2, ; 123: System.Reflection.Emit.Lightweight => 96
	i64 u0x36cada77dc79928b, ; 124: System.IO.MemoryMappedFiles => 55
	i64 u0x374ef46b06791af6, ; 125: System.Reflection.Primitives.dll => 100
	i64 u0x37bc29f3183003b6, ; 126: lib_System.IO.dll.so => 60
	i64 u0x380134e03b1e160a, ; 127: System.Collections.Immutable.dll => 11
	i64 u0x38049b5c59b39324, ; 128: System.Runtime.CompilerServices.Unsafe => 106
	i64 u0x38869c811d74050e, ; 129: System.Net.NameResolution.dll => 71
	i64 u0x3ab5859054645f72, ; 130: System.Security.Cryptography.Primitives.dll => 129
	i64 u0x3ae44ac43a1fbdbb, ; 131: System.Runtime.Serialization => 120
	i64 u0x3b860f9932505633, ; 132: lib_System.Text.Encoding.Extensions.dll.so => 139
	i64 u0x3c3aafb6b3a00bf6, ; 133: lib_System.Security.Cryptography.X509Certificates.dll.so => 130
	i64 u0x3c4049146b59aa90, ; 134: System.Runtime.InteropServices.JavaScript => 110
	i64 u0x3c7e5ed3d5db71bb, ; 135: System.Security => 135
	i64 u0x3d2b1913edfc08d7, ; 136: lib_System.Threading.ThreadPool.dll.so => 152
	i64 u0x3d46f0b995082740, ; 137: System.Xml.Linq => 161
	i64 u0x3e57d4d195c53c2e, ; 138: System.Reflection.TypeExtensions => 101
	i64 u0x3e616ab4ed1f3f15, ; 139: lib_System.Data.dll.so => 26
	i64 u0x3f510adf788828dd, ; 140: System.Threading.Tasks.Extensions => 148
	i64 u0x40c98b6bd77346d4, ; 141: Microsoft.VisualBasic.dll => 5
	i64 u0x41833cf766d27d96, ; 142: mscorlib => 172
	i64 u0x423a9ecc4d905a88, ; 143: lib_System.Resources.ResourceManager.dll.so => 104
	i64 u0x423bf51ae7def810, ; 144: System.Xml.XPath => 166
	i64 u0x42462ff15ddba223, ; 145: System.Resources.Reader.dll => 103
	i64 u0x42a31b86e6ccc3f0, ; 146: System.Diagnostics.Contracts => 27
	i64 u0x430e95b891249788, ; 147: lib_System.Reflection.Emit.dll.so => 97
	i64 u0x43375950ec7c1b6a, ; 148: netstandard.dll => 173
	i64 u0x434c4e1d9284cdae, ; 149: Mono.Android.dll => 177
	i64 u0x437d06c381ed575a, ; 150: lib_Microsoft.VisualBasic.dll.so => 5
	i64 u0x448bd33429269b19, ; 151: Microsoft.CSharp => 3
	i64 u0x4499fa3c8e494654, ; 152: lib_System.Runtime.Serialization.Primitives.dll.so => 118
	i64 u0x45c40276a42e283e, ; 153: System.Diagnostics.TraceSource => 35
	i64 u0x45d443f2a29adc37, ; 154: System.AppContext.dll => 8
	i64 u0x47358bd471172e1d, ; 155: lib_System.Xml.Linq.dll.so => 161
	i64 u0x480c0a47dd42dd81, ; 156: lib_System.IO.MemoryMappedFiles.dll.so => 55
	i64 u0x490cffb50a3cc73e, ; 157: MonoGame.Framework => 180
	i64 u0x49e952f19a4e2022, ; 158: System.ObjectModel => 89
	i64 u0x4a7a18981dbd56bc, ; 159: System.IO.Compression.FileSystem.dll => 46
	i64 u0x4b07a0ed0ab33ff4, ; 160: System.Runtime.Extensions.dll => 108
	i64 u0x4b576d47ac054f3c, ; 161: System.IO.FileSystem.AccessControl => 49
	i64 u0x4b7b6532ded934b7, ; 162: System.Text.Json => 142
	i64 u0x4c7755cf07ad2d5f, ; 163: System.Net.Http.Json.dll => 67
	i64 u0x4cf6f67dc77aacd2, ; 164: System.Net.NetworkInformation.dll => 72
	i64 u0x4d3183dd245425d4, ; 165: System.Net.WebSockets.Client.dll => 84
	i64 u0x4d479f968a05e504, ; 166: System.Linq.Expressions.dll => 62
	i64 u0x4d55a010ffc4faff, ; 167: System.Private.Xml => 93
	i64 u0x4d5cbe77561c5b2e, ; 168: System.Web.dll => 159
	i64 u0x4d7793536e79c309, ; 169: System.ServiceProcess => 137
	i64 u0x4d95fccc1f67c7ca, ; 170: System.Runtime.Loader.dll => 114
	i64 u0x4db014bf0ff1c9c1, ; 171: System.Linq.AsyncEnumerable => 61
	i64 u0x4e32f00cb0937401, ; 172: Mono.Android.Runtime => 176
	i64 u0x4e5eea4668ac2b18, ; 173: System.Text.Encoding.CodePages => 138
	i64 u0x4ebd0c4b82c5eefc, ; 174: lib_System.Threading.Channels.dll.so => 145
	i64 u0x4ee8eaa9c9c1151a, ; 175: System.Globalization.Calendars => 42
	i64 u0x50c3a29b21050d45, ; 176: System.Linq.Parallel.dll => 63
	i64 u0x516324a5050a7e3c, ; 177: System.Net.WebProxy => 83
	i64 u0x516d6f0b21a303de, ; 178: lib_System.Diagnostics.Contracts.dll.so => 27
	i64 u0x51bb8a2afe774e32, ; 179: System.Drawing => 38
	i64 u0x5247c5c32a4140f0, ; 180: System.Resources.Reader => 103
	i64 u0x526ce79eb8e90527, ; 181: lib_System.Net.Primitives.dll.so => 74
	i64 u0x52829f00b4467c38, ; 182: lib_System.Data.Common.dll.so => 24
	i64 u0x53978aac584c666e, ; 183: lib_System.Security.Cryptography.Cng.dll.so => 125
	i64 u0x53a96d5c86c9e194, ; 184: System.Net.NetworkInformation => 72
	i64 u0x53be1038a61e8d44, ; 185: System.Runtime.InteropServices.RuntimeInformation.dll => 111
	i64 u0x5435e6f049e9bc37, ; 186: System.Security.Claims.dll => 123
	i64 u0x54795225dd1587af, ; 187: lib_System.Runtime.dll.so => 121
	i64 u0x5588627c9a108ec9, ; 188: System.Collections.Specialized => 13
	i64 u0x55a898e4f42e3fae, ; 189: Microsoft.VisualBasic.Core.dll => 4
	i64 u0x55fa0c610fe93bb1, ; 190: lib_System.Security.Cryptography.OpenSsl.dll.so => 128
	i64 u0x56442b99bc64bb47, ; 191: System.Runtime.Serialization.Xml.dll => 119
	i64 u0x56a8b26e1aeae27b, ; 192: System.Threading.Tasks.Dataflow => 147
	i64 u0x56f932d61e93c07f, ; 193: System.Globalization.Extensions => 43
	i64 u0x571c5cfbec5ae8e2, ; 194: System.Private.Uri => 91
	i64 u0x579a06fed6eec900, ; 195: System.Private.CoreLib.dll => 179
	i64 u0x57c542c14049b66d, ; 196: System.Diagnostics.DiagnosticSource => 29
	i64 u0x581a8bd5cfda563e, ; 197: System.Threading.Timer => 153
	i64 u0x595a356d23e8da9a, ; 198: lib_Microsoft.CSharp.dll.so => 3
	i64 u0x5a745f5101a75527, ; 199: lib_System.IO.Compression.FileSystem.dll.so => 46
	i64 u0x5a8f6699f4a1caa9, ; 200: lib_System.Threading.dll.so => 154
	i64 u0x5ae9cd33b15841bf, ; 201: System.ComponentModel => 20
	i64 u0x5b54391bdc6fcfe6, ; 202: System.Private.DataContractSerialization => 90
	i64 u0x5b8109e8e14c5e3e, ; 203: System.Globalization.Extensions.dll => 43
	i64 u0x5c30a4a35f9cc8c4, ; 204: lib_System.Reflection.Extensions.dll.so => 98
	i64 u0x5c53c29f5073b0c9, ; 205: System.Diagnostics.FileVersionInfo => 30
	i64 u0x5c87463c575c7616, ; 206: lib_System.Globalization.Extensions.dll.so => 43
	i64 u0x5d0a4a29b02d9d3c, ; 207: System.Net.WebHeaderCollection.dll => 82
	i64 u0x5d7ec76c1c703055, ; 208: System.Threading.Tasks.Parallel => 149
	i64 u0x5db0cbbd1028510e, ; 209: lib_System.Runtime.InteropServices.dll.so => 112
	i64 u0x5e467bc8f09ad026, ; 210: System.Collections.Specialized.dll => 13
	i64 u0x5e5173b3208d97e7, ; 211: System.Runtime.Handles.dll => 109
	i64 u0x5ea92fdb19ec8c4c, ; 212: System.Text.Encodings.Web.dll => 141
	i64 u0x5eb8046dd40e9ac3, ; 213: System.ComponentModel.Primitives => 18
	i64 u0x5ec272d219c9aba4, ; 214: System.Security.Cryptography.Csp.dll => 126
	i64 u0x5eee1376d94c7f5e, ; 215: System.Net.HttpListener.dll => 69
	i64 u0x5f36ccf5c6a57e24, ; 216: System.Xml.ReaderWriter.dll => 162
	i64 u0x5f4294b9b63cb842, ; 217: System.Data.Common => 24
	i64 u0x5fac98e0b37a5b9d, ; 218: System.Runtime.CompilerServices.Unsafe.dll => 106
	i64 u0x60f62d786afcf130, ; 219: System.Memory => 66
	i64 u0x61bb78c89f867353, ; 220: System.IO => 60
	i64 u0x61d88f399afb2f45, ; 221: lib_System.Runtime.Loader.dll.so => 114
	i64 u0x622eef6f9e59068d, ; 222: System.Private.CoreLib => 179
	i64 u0x62f4599edc7f34af, ; 223: merder.dll => 2
	i64 u0x63f1f6883c1e23c2, ; 224: lib_System.Collections.Immutable.dll.so => 11
	i64 u0x640e3b14dbd325c2, ; 225: System.Security.Cryptography.Algorithms.dll => 124
	i64 u0x64587004560099b9, ; 226: System.Reflection => 102
	i64 u0x64b1529a438a3c45, ; 227: lib_System.Runtime.Handles.dll.so => 109
	i64 u0x64b61dd9da8a4d57, ; 228: System.Net.ServerSentEvents.dll => 78
	i64 u0x65ece51227bfa724, ; 229: lib_System.Runtime.Numerics.dll.so => 115
	i64 u0x6679b2337ee6b22a, ; 230: lib_System.IO.FileSystem.Primitives.dll.so => 51
	i64 u0x667c66a03dd97d40, ; 231: System.Linq.AsyncEnumerable.dll => 61
	i64 u0x6692e924eade1b29, ; 232: lib_System.Console.dll.so => 22
	i64 u0x674303f65d8fad6f, ; 233: lib_System.Net.Quic.dll.so => 75
	i64 u0x67c0802770244408, ; 234: System.Windows.dll => 160
	i64 u0x68100b69286e27cd, ; 235: lib_System.Formats.Tar.dll.so => 41
	i64 u0x6872ec7a2e36b1ac, ; 236: System.Drawing.Primitives.dll => 37
	i64 u0x68fbbbe2eb455198, ; 237: System.Formats.Asn1 => 40
	i64 u0x6a4d7577b2317255, ; 238: System.Runtime.InteropServices.dll => 112
	i64 u0x6afcedb171067e2b, ; 239: System.Core.dll => 23
	i64 u0x6d70755158ca866e, ; 240: lib_System.ComponentModel.EventBasedAsync.dll.so => 17
	i64 u0x6d7eeca99577fc8b, ; 241: lib_System.Net.WebProxy.dll.so => 83
	i64 u0x6d8515b19946b6a2, ; 242: System.Net.WebProxy.dll => 83
	i64 u0x6e838d9a2a6f6c9e, ; 243: lib_System.ValueTuple.dll.so => 157
	i64 u0x6e9965ce1095e60a, ; 244: lib_System.Core.dll.so => 23
	i64 u0x6ffc4967cc47ba57, ; 245: System.IO.FileSystem.Watcher.dll => 52
	i64 u0x701cd46a1c25a5fe, ; 246: System.IO.FileSystem.dll => 53
	i64 u0x71485e7ffdb4b958, ; 247: System.Reflection.Extensions => 98
	i64 u0x71ad672adbe48f35, ; 248: System.ComponentModel.Primitives.dll => 18
	i64 u0x725f5a9e82a45c81, ; 249: System.Security.Cryptography.Encoding => 127
	i64 u0x72e0300099accce1, ; 250: System.Xml.XPath.XDocument => 165
	i64 u0x730bfb248998f67a, ; 251: System.IO.Compression.ZipFile => 47
	i64 u0x73a6be34e822f9d1, ; 252: lib_System.Runtime.Serialization.dll.so => 120
	i64 u0x73e4ce94e2eb6ffc, ; 253: lib_System.Memory.dll.so => 66
	i64 u0x743a1eccf080489a, ; 254: WindowsBase.dll => 171
	i64 u0x75c326eb821b85c4, ; 255: lib_System.ComponentModel.DataAnnotations.dll.so => 16
	i64 u0x76ca07b878f44da0, ; 256: System.Runtime.Numerics.dll => 115
	i64 u0x778a805e625329ef, ; 257: System.Linq.Parallel => 63
	i64 u0x77d9074d8f33a303, ; 258: lib_System.Net.ServerSentEvents.dll.so => 78
	i64 u0x77f8a4acc2fdc449, ; 259: System.Security.Cryptography.Cng.dll => 125
	i64 u0x782c5d8eb99ff201, ; 260: lib_Microsoft.VisualBasic.Core.dll.so => 4
	i64 u0x7a9a57d43b0845fa, ; 261: System.AppContext => 8
	i64 u0x7bef86a4335c4870, ; 262: System.ComponentModel.TypeConverter => 19
	i64 u0x7c41d387501568ba, ; 263: System.Net.WebClient.dll => 81
	i64 u0x7cd2ec8eaf5241cd, ; 264: System.Security.dll => 135
	i64 u0x7d8ee2bdc8e3aad1, ; 265: System.Numerics.Vectors => 87
	i64 u0x7dfc3d6d9d8d7b70, ; 266: System.Collections => 14
	i64 u0x7e2e564fa2f76c65, ; 267: lib_System.Diagnostics.Tracing.dll.so => 36
	i64 u0x7e302e110e1e1346, ; 268: lib_System.Security.Claims.dll.so => 123
	i64 u0x7e6ac99e4e8df72f, ; 269: System.IO.Hashing => 178
	i64 u0x7e946809d6008ef2, ; 270: lib_System.ObjectModel.dll.so => 89
	i64 u0x7ecc13347c8fd849, ; 271: lib_System.ComponentModel.dll.so => 20
	i64 u0x8076a9a44a2ca331, ; 272: System.Net.Quic => 75
	i64 u0x80da183a87731838, ; 273: System.Reflection.Metadata => 99
	i64 u0x812c069d5cdecc17, ; 274: System.dll => 170
	i64 u0x81657cec2b31e8aa, ; 275: System.Net => 86
	i64 u0x82b399cb01b531c4, ; 276: lib_System.Web.dll.so => 159
	i64 u0x82df8f5532a10c59, ; 277: lib_System.Drawing.dll.so => 38
	i64 u0x82f0b6e911d13535, ; 278: lib_System.Transactions.dll.so => 156
	i64 u0x846ce984efea52c7, ; 279: System.Threading.Tasks.Parallel.dll => 149
	i64 u0x84ae73148a4557d2, ; 280: lib_System.IO.Pipes.dll.so => 58
	i64 u0x84b01102c12a9232, ; 281: System.Runtime.Serialization.Json.dll => 117
	i64 u0x8662aaeb94fef37f, ; 282: lib_System.Dynamic.Runtime.dll.so => 39
	i64 u0x86b62cb077ec4fd7, ; 283: System.Runtime.Serialization.Xml => 119
	i64 u0x872a5b14c18d328c, ; 284: System.ComponentModel.DataAnnotations => 16
	i64 u0x87c1695b70e5f714, ; 285: lib_merder.Core.dll.so => 181
	i64 u0x87c69b87d9283884, ; 286: lib_System.Threading.Thread.dll.so => 151
	i64 u0x87f6569b25707834, ; 287: System.IO.Compression.Brotli.dll => 45
	i64 u0x88ba6bc4f7762b03, ; 288: lib_System.Reflection.dll.so => 102
	i64 u0x8930322c7bd8f768, ; 289: netstandard => 173
	i64 u0x897a606c9e39c75f, ; 290: lib_System.ComponentModel.Primitives.dll.so => 18
	i64 u0x89911a22005b92b7, ; 291: System.IO.FileSystem.DriveInfo.dll => 50
	i64 u0x89c5188089ec2cd5, ; 292: lib_System.Runtime.InteropServices.RuntimeInformation.dll.so => 111
	i64 u0x8a19e3dc71b34b2c, ; 293: System.Reflection.TypeExtensions.dll => 101
	i64 u0x8b4ff5d0fdd5faa1, ; 294: lib_System.Diagnostics.DiagnosticSource.dll.so => 29
	i64 u0x8b541d476eb3774c, ; 295: System.Security.Principal.Windows => 132
	i64 u0x8b8d01333a96d0b5, ; 296: System.Diagnostics.Process.dll => 31
	i64 u0x8cdfdb4ce85fb925, ; 297: lib_System.Security.Principal.Windows.dll.so => 132
	i64 u0x8cdfe7b8f4caa426, ; 298: System.IO.Compression.FileSystem => 46
	i64 u0x8d7b8ab4b3310ead, ; 299: System.Threading => 154
	i64 u0x8da188285aadfe8e, ; 300: System.Collections.Concurrent => 10
	i64 u0x8f44b45eb046bbd1, ; 301: System.ServiceModel.Web.dll => 136
	i64 u0x8fbf5b0114c6dcef, ; 302: System.Globalization.dll => 44
	i64 u0x90263f8448b8f572, ; 303: lib_System.Diagnostics.TraceSource.dll.so => 35
	i64 u0x903101b46fb73a04, ; 304: _Microsoft.Android.Resource.Designer => 182
	i64 u0x90393bd4865292f3, ; 305: lib_System.IO.Compression.dll.so => 48
	i64 u0x905e2b8e7ae91ae6, ; 306: System.Threading.Tasks.Extensions.dll => 148
	i64 u0x90da922f4ccbc487, ; 307: lib_MonoGame.Framework.dll.so => 180
	i64 u0x9157bd523cd7ed36, ; 308: lib_System.Text.Json.dll.so => 142
	i64 u0x91a74f07b30d37e2, ; 309: System.Linq.dll => 65
	i64 u0x91cb86ea3b17111d, ; 310: System.ServiceModel.Web => 136
	i64 u0x92054e486c0c7ea7, ; 311: System.IO.FileSystem.DriveInfo => 50
	i64 u0x928614058c40c4cd, ; 312: lib_System.Xml.XPath.XDocument.dll.so => 165
	i64 u0x944077d8ca3c6580, ; 313: System.IO.Compression.dll => 48
	i64 u0x948cffedc8ed7960, ; 314: System.Xml => 169
	i64 u0x94bbeab0d4764588, ; 315: System.IO.Hashing.dll => 178
	i64 u0x97b8c771ea3e4220, ; 316: System.ComponentModel.dll => 20
	i64 u0x97e144c9d3c6976e, ; 317: System.Collections.Concurrent.dll => 10
	i64 u0x98d720cc4597562c, ; 318: System.Security.Cryptography.OpenSsl => 128
	i64 u0x991d510397f92d9d, ; 319: System.Linq.Expressions => 62
	i64 u0x996ceeb8a3da3d67, ; 320: System.Threading.Overlapped.dll => 146
	i64 u0x9b211a749105beac, ; 321: System.Transactions.Local => 155
	i64 u0x9b8734714671022d, ; 322: System.Threading.Tasks.Dataflow.dll => 147
	i64 u0x9c244ac7cda32d26, ; 323: System.Security.Cryptography.X509Certificates.dll => 130
	i64 u0x9c8f6872beab6408, ; 324: System.Xml.XPath.XDocument.dll => 165
	i64 u0x9ce01cf91101ae23, ; 325: System.Xml.XmlDocument => 167
	i64 u0x9e4b95dec42769f7, ; 326: System.Diagnostics.Debug.dll => 28
	i64 u0xa00832eb975f56a8, ; 327: lib_System.Net.dll.so => 86
	i64 u0xa0d8259f4cc284ec, ; 328: lib_System.Security.Cryptography.dll.so => 131
	i64 u0xa0ff9b3e34d92f11, ; 329: lib_System.Resources.Writer.dll.so => 105
	i64 u0xa12fbfb4da97d9f3, ; 330: System.Threading.Timer.dll => 153
	i64 u0xa2572680829d2c7c, ; 331: System.IO.Pipelines.dll => 56
	i64 u0xa26597e57ee9c7f6, ; 332: System.Xml.XmlDocument.dll => 167
	i64 u0xa308401900e5bed3, ; 333: lib_mscorlib.dll.so => 172
	i64 u0xa395572e7da6c99d, ; 334: lib_System.Security.dll.so => 135
	i64 u0xa3e683f24b43af6f, ; 335: System.Dynamic.Runtime.dll => 39
	i64 u0xa4edc8f2ceae241a, ; 336: System.Data.Common.dll => 24
	i64 u0xa5494f40f128ce6a, ; 337: System.Runtime.Serialization.Formatters.dll => 116
	i64 u0xa54b74df83dce92b, ; 338: System.Reflection.DispatchProxy => 94
	i64 u0xa5b7152421ed6d98, ; 339: lib_System.IO.FileSystem.Watcher.dll.so => 52
	i64 u0xa5c3844f17b822db, ; 340: lib_System.Linq.Parallel.dll.so => 63
	i64 u0xa5ce5c755bde8cb8, ; 341: lib_System.Security.Cryptography.Csp.dll.so => 126
	i64 u0xa5e599d1e0524750, ; 342: System.Numerics.Vectors.dll => 87
	i64 u0xa5f1ba49b85dd355, ; 343: System.Security.Cryptography.dll => 131
	i64 u0xa61975a5a37873ea, ; 344: lib_System.Xml.XmlSerializer.dll.so => 168
	i64 u0xa62e8d5c29c329c0, ; 345: lib-es-ES-merder.Core.resources.dll.so => 0
	i64 u0xa66cbee0130865f7, ; 346: lib_WindowsBase.dll.so => 171
	i64 u0xa763fbb98df8d9fb, ; 347: lib_Microsoft.Win32.Primitives.dll.so => 6
	i64 u0xa7eab29ed44b4e7a, ; 348: Mono.Android.Export => 175
	i64 u0xa8195217cbf017b7, ; 349: Microsoft.VisualBasic.Core => 4
	i64 u0xa8b52f21e0dbe690, ; 350: System.Runtime.Serialization.dll => 120
	i64 u0xa95590e7c57438a4, ; 351: System.Configuration => 21
	i64 u0xaa443ac34067eeef, ; 352: System.Private.Xml.dll => 93
	i64 u0xaa52de307ef5d1dd, ; 353: System.Net.Http => 68
	i64 u0xaa9a7b0214a5cc5c, ; 354: System.Diagnostics.StackTrace.dll => 32
	i64 u0xab9c1b2687d86b0b, ; 355: lib_System.Linq.Expressions.dll.so => 62
	i64 u0xac2af3fa195a15ce, ; 356: System.Runtime.Numerics => 115
	i64 u0xac5acae88f60357e, ; 357: System.Diagnostics.Tools.dll => 34
	i64 u0xac79c7e46047ad98, ; 358: System.Security.Principal.Windows.dll => 132
	i64 u0xac98d31068e24591, ; 359: System.Xml.XDocument => 164
	i64 u0xacf42eea7ef9cd12, ; 360: System.Threading.Channels => 145
	i64 u0xadbb53caf78a79d2, ; 361: System.Web.HttpUtility => 158
	i64 u0xadc90ab061a9e6e4, ; 362: System.ComponentModel.TypeConverter.dll => 19
	i64 u0xadf4cf30debbeb9a, ; 363: System.Net.ServicePoint.dll => 79
	i64 u0xadf511667bef3595, ; 364: System.Net.Security => 77
	i64 u0xae0aaa94fdcfce0f, ; 365: System.ComponentModel.EventBasedAsync.dll => 17
	i64 u0xae282bcd03739de7, ; 366: Java.Interop => 174
	i64 u0xae3cf00a7be5ff7b, ; 367: es-ES/merder.Core.resources => 0
	i64 u0xae53579c90db1107, ; 368: System.ObjectModel.dll => 89
	i64 u0xaf732d0b2193b8f5, ; 369: System.Security.Cryptography.OpenSsl.dll => 128
	i64 u0xb0bb43dc52ea59f9, ; 370: System.Diagnostics.Tracing.dll => 36
	i64 u0xb1dd05401aa8ee63, ; 371: System.Security.AccessControl => 122
	i64 u0xb220631954820169, ; 372: System.Text.RegularExpressions => 143
	i64 u0xb2376e1dbf8b4ed7, ; 373: System.Security.Cryptography.Csp => 126
	i64 u0xb2a1959fe95c5402, ; 374: lib_System.Runtime.InteropServices.JavaScript.dll.so => 110
	i64 u0xb4bd7015ecee9d86, ; 375: System.IO.Pipelines => 56
	i64 u0xb4c53d9749c5f226, ; 376: lib_System.IO.FileSystem.AccessControl.dll.so => 49
	i64 u0xb4ff710863453fda, ; 377: System.Diagnostics.FileVersionInfo.dll => 30
	i64 u0xb54092076b15e062, ; 378: System.Threading.AccessControl => 144
	i64 u0xb5c38bf497a4cfe2, ; 379: lib_System.Threading.Tasks.dll.so => 150
	i64 u0xb5ea31d5244c6626, ; 380: System.Threading.ThreadPool.dll => 152
	i64 u0xb7212c4683a94afe, ; 381: System.Drawing.Primitives => 37
	i64 u0xb81a2c6e0aee50fe, ; 382: lib_System.Private.CoreLib.dll.so => 179
	i64 u0xb8c60af47c08d4da, ; 383: System.Net.ServicePoint => 79
	i64 u0xb8e68d20aad91196, ; 384: lib_System.Xml.XPath.dll.so => 166
	i64 u0xb9185c33a1643eed, ; 385: Microsoft.CSharp.dll => 3
	i64 u0xba4670aa94a2b3c6, ; 386: lib_System.Xml.XDocument.dll.so => 164
	i64 u0xba48785529705af9, ; 387: System.Collections.dll => 14
	i64 u0xba965b8c86359996, ; 388: lib_System.Windows.dll.so => 160
	i64 u0xbb286883bc35db36, ; 389: System.Transactions.dll => 156
	i64 u0xbb65706fde942ce3, ; 390: System.Net.Sockets => 80
	i64 u0xbba28979413cad9e, ; 391: lib_System.Runtime.CompilerServices.VisualC.dll.so => 107
	i64 u0xbbd180354b67271a, ; 392: System.Runtime.Serialization.Formatters => 116
	i64 u0xbd0e2c0d55246576, ; 393: System.Net.Http.dll => 68
	i64 u0xbd3fbd85b9e1cb29, ; 394: lib_System.Net.HttpListener.dll.so => 69
	i64 u0xbd4f572d2bd0a789, ; 395: System.IO.Compression.ZipFile.dll => 47
	i64 u0xbd877b14d0b56392, ; 396: System.Runtime.Intrinsics.dll => 113
	i64 u0xbe65a49036345cf4, ; 397: lib_System.Buffers.dll.so => 9
	i64 u0xbef9919db45b4ca7, ; 398: System.IO.Pipes.AccessControl => 57
	i64 u0xbfc1e1fb3095f2b3, ; 399: lib_System.Net.Http.Json.dll.so => 67
	i64 u0xc05794dfa82aa1ad, ; 400: lib-fr-FR-merder.Core.resources.dll.so => 1
	i64 u0xc0d928351ab5ca77, ; 401: System.Console.dll => 22
	i64 u0xc0f5a221a9383aea, ; 402: System.Runtime.Intrinsics => 113
	i64 u0xc111030af54d7191, ; 403: System.Resources.Writer => 105
	i64 u0xc12b8b3afa48329c, ; 404: lib_System.Linq.dll.so => 65
	i64 u0xc183ca0b74453aa9, ; 405: lib_System.Threading.Tasks.Dataflow.dll.so => 147
	i64 u0xc26c064effb1dea9, ; 406: System.Buffers.dll => 9
	i64 u0xc2902f6cf5452577, ; 407: lib_Mono.Android.Export.dll.so => 175
	i64 u0xc2a3bca55b573141, ; 408: System.IO.FileSystem.Watcher => 52
	i64 u0xc30b52815b58ac2c, ; 409: lib_System.Runtime.Serialization.Xml.dll.so => 119
	i64 u0xc323dd2b7e5d1ef5, ; 410: es-ES/merder.Core.resources.dll => 0
	i64 u0xc36d7d89c652f455, ; 411: System.Threading.Overlapped => 146
	i64 u0xc3c86c1e5e12f03d, ; 412: WindowsBase => 171
	i64 u0xc421b61fd853169d, ; 413: lib_System.Net.WebSockets.Client.dll.so => 84
	i64 u0xc463e077917aa21d, ; 414: System.Runtime.Serialization.Json => 117
	i64 u0xc50fded0ded1418c, ; 415: lib_System.ComponentModel.TypeConverter.dll.so => 19
	i64 u0xc519125d6bc8fb11, ; 416: lib_System.Net.Requests.dll.so => 76
	i64 u0xc5325b2fcb37446f, ; 417: lib_System.Private.Xml.dll.so => 93
	i64 u0xc5a0f4b95a699af7, ; 418: lib_System.Private.Uri.dll.so => 91
	i64 u0xc5cdcd5b6277579e, ; 419: lib_System.Security.Cryptography.Algorithms.dll.so => 124
	i64 u0xc7c01e7d7c93a110, ; 420: System.Text.Encoding.Extensions.dll => 139
	i64 u0xc7ce851898a4548e, ; 421: lib_System.Web.HttpUtility.dll.so => 158
	i64 u0xc809d4089d2556b2, ; 422: System.Runtime.InteropServices.JavaScript.dll => 110
	i64 u0xc858a28d9ee5a6c5, ; 423: lib_System.Collections.Specialized.dll.so => 13
	i64 u0xc8ac7c6bf1c2ec51, ; 424: System.Reflection.DispatchProxy.dll => 94
	i64 u0xc9c62c8f354ac568, ; 425: lib_System.Diagnostics.TextWriterTraceListener.dll.so => 33
	i64 u0xca5801070d9fccfb, ; 426: System.Text.Encoding => 140
	i64 u0xcbb5f80c7293e696, ; 427: lib_System.Globalization.Calendars.dll.so => 42
	i64 u0xcbd4fdd9cef4a294, ; 428: lib__Microsoft.Android.Resource.Designer.dll.so => 182
	i64 u0xcc2876b32ef2794c, ; 429: lib_System.Text.RegularExpressions.dll.so => 143
	i64 u0xcc667334debe5bbf, ; 430: MonoGame.Framework.dll => 180
	i64 u0xcc9fa2923aa1c9ef, ; 431: System.Diagnostics.Contracts.dll => 27
	i64 u0xccae9bb73e2326bd, ; 432: lib_System.IO.Hashing.dll.so => 178
	i64 u0xcd10a42808629144, ; 433: System.Net.Requests => 76
	i64 u0xcf23d8093f3ceadf, ; 434: System.Diagnostics.DiagnosticSource.dll => 29
	i64 u0xcf5ff6b6b2c4c382, ; 435: System.Net.Mail.dll => 70
	i64 u0xcf8fc898f98b0d34, ; 436: System.Private.Xml.Linq => 92
	i64 u0xd04b5f59ed596e31, ; 437: System.Reflection.Metadata.dll => 99
	i64 u0xd063299fcfc0c93f, ; 438: lib_System.Runtime.Serialization.Json.dll.so => 117
	i64 u0xd0de8a113e976700, ; 439: System.Diagnostics.TextWriterTraceListener => 33
	i64 u0xd0fc33d5ae5d4cb8, ; 440: System.Runtime.Extensions => 108
	i64 u0xd12beacdfc14f696, ; 441: System.Dynamic.Runtime => 39
	i64 u0xd198e7ce1b6a8344, ; 442: System.Net.Quic.dll => 75
	i64 u0xd333d0af9e423810, ; 443: System.Runtime.InteropServices => 112
	i64 u0xd33a415cb4278969, ; 444: System.Security.Cryptography.Encoding.dll => 127
	i64 u0xd3651b6fc3125825, ; 445: System.Private.Uri.dll => 91
	i64 u0xd3801faafafb7698, ; 446: System.Private.DataContractSerialization.dll => 90
	i64 u0xd3edcc1f25459a50, ; 447: System.Reflection.Emit => 97
	i64 u0xd4fa0abb79079ea9, ; 448: System.Security.Principal.dll => 133
	i64 u0xd5ee6055d19149ba, ; 449: merder => 2
	i64 u0xd6cdf574a2cf8965, ; 450: fr-FR/merder.Core.resources => 1
	i64 u0xd72c760af136e863, ; 451: System.Xml.XmlSerializer.dll => 168
	i64 u0xd753f071e44c2a03, ; 452: lib_System.Security.SecureString.dll.so => 134
	i64 u0xdad05a11827959a3, ; 453: System.Collections.NonGeneric.dll => 12
	i64 u0xdaefdfe71aa53cf9, ; 454: System.IO.FileSystem.Primitives => 51
	i64 u0xdb58816721c02a59, ; 455: lib_System.Reflection.Emit.ILGeneration.dll.so => 95
	i64 u0xdbf2a779fbc3ac31, ; 456: System.Transactions.Local.dll => 155
	i64 u0xdbf9607a441b4505, ; 457: System.Linq => 65
	i64 u0xdbfc90157a0de9b0, ; 458: lib_System.Text.Encoding.dll.so => 140
	i64 u0xdc75032002d1a212, ; 459: lib_System.Transactions.Local.dll.so => 155
	i64 u0xdca8be7403f92d4f, ; 460: lib_System.Linq.Queryable.dll.so => 64
	i64 u0xdd2b722d78ef5f43, ; 461: System.Runtime.dll => 121
	i64 u0xdd67031857c72f96, ; 462: lib_System.Text.Encodings.Web.dll.so => 141
	i64 u0xdd92e229ad292030, ; 463: System.Numerics.dll => 88
	i64 u0xde110ae80fa7c2e2, ; 464: System.Xml.XDocument.dll => 164
	i64 u0xde4bd7b6e3c8772d, ; 465: merder.Core.dll => 181
	i64 u0xde572c2b2fb32f93, ; 466: lib_System.Threading.Tasks.Extensions.dll.so => 148
	i64 u0xdf4b773de8fb1540, ; 467: System.Net.dll => 86
	i64 u0xdf9c7682560a9629, ; 468: System.Net.ServerSentEvents => 78
	i64 u0xdfa254ebb4346068, ; 469: System.Net.Ping => 73
	i64 u0xe021eaa401792a05, ; 470: System.Text.Encoding.dll => 140
	i64 u0xe10b760bb1462e7a, ; 471: lib_System.Security.Cryptography.Primitives.dll.so => 129
	i64 u0xe192a588d4410686, ; 472: lib_System.IO.Pipelines.dll.so => 56
	i64 u0xe1a08bd3fa539e0d, ; 473: System.Runtime.Loader => 114
	i64 u0xe1a77eb8831f7741, ; 474: System.Security.SecureString.dll => 134
	i64 u0xe1b52f9f816c70ef, ; 475: System.Private.Xml.Linq.dll => 92
	i64 u0xe1e199c8ab02e356, ; 476: System.Data.DataSetExtensions.dll => 25
	i64 u0xe1ecfdb7fff86067, ; 477: System.Net.Security.dll => 77
	i64 u0xe2252a80fe853de4, ; 478: lib_System.Security.Principal.dll.so => 133
	i64 u0xe22fa4c9c645db62, ; 479: System.Diagnostics.TextWriterTraceListener.dll => 33
	i64 u0xe2420585aeceb728, ; 480: System.Net.Requests.dll => 76
	i64 u0xe2ad448dee50fbdf, ; 481: System.Xml.Serialization => 163
	i64 u0xe2d920f978f5d85c, ; 482: System.Data.DataSetExtensions => 25
	i64 u0xe2e426c7714fa0bc, ; 483: Microsoft.Win32.Primitives.dll => 6
	i64 u0xe332bacb3eb4a806, ; 484: Mono.Android.Export.dll => 175
	i64 u0xe3b7cbae5ad66c75, ; 485: lib_System.Security.Cryptography.Encoding.dll.so => 127
	i64 u0xe4f74a0b5bf9703f, ; 486: System.Runtime.Serialization.Primitives => 118
	i64 u0xe5434e8a119ceb69, ; 487: lib_Mono.Android.dll.so => 177
	i64 u0xe55703b9ce5c038a, ; 488: System.Diagnostics.Tools => 34
	i64 u0xe57013c8afc270b5, ; 489: Microsoft.VisualBasic => 5
	i64 u0xe62913cc36bc07ec, ; 490: System.Xml.dll => 169
	i64 u0xe7e03cc18dcdeb49, ; 491: lib_System.Diagnostics.StackTrace.dll.so => 32
	i64 u0xe7e147ff99a7a380, ; 492: lib_System.Configuration.dll.so => 21
	i64 u0xe896622fe0902957, ; 493: System.Reflection.Emit.dll => 97
	i64 u0xe89a2a9ef110899b, ; 494: System.Drawing.dll => 38
	i64 u0xe8c5f8c100b5934b, ; 495: Microsoft.Win32.Registry => 7
	i64 u0xe9b9c8c0458fd92a, ; 496: System.Windows => 160
	i64 u0xedc4817167106c23, ; 497: System.Net.Sockets.dll => 80
	i64 u0xedc632067fb20ff3, ; 498: System.Memory.dll => 66
	i64 u0xee81f5b3f1c4f83b, ; 499: System.Threading.ThreadPool => 152
	i64 u0xeefc635595ef57f0, ; 500: System.Security.Cryptography.Cng => 125
	i64 u0xef03b1b5a04e9709, ; 501: System.Text.Encoding.CodePages.dll => 138
	i64 u0xefd1e0c4e5c9b371, ; 502: System.Resources.ResourceManager.dll => 104
	i64 u0xefe8f8d5ed3c72ea, ; 503: System.Formats.Tar.dll => 41
	i64 u0xeff59cbde4363ec3, ; 504: System.Threading.AccessControl.dll => 144
	i64 u0xf09e47b6ae914f6e, ; 505: System.Net.NameResolution => 71
	i64 u0xf0ac2b489fed2e35, ; 506: lib_System.Diagnostics.Debug.dll.so => 28
	i64 u0xf0bb49dadd3a1fe1, ; 507: lib_System.Net.ServicePoint.dll.so => 79
	i64 u0xf0de2537ee19c6ca, ; 508: lib_System.Net.WebHeaderCollection.dll.so => 82
	i64 u0xf161f4f3c3b7e62c, ; 509: System.Data => 26
	i64 u0xf16eb650d5a464bc, ; 510: System.ValueTuple => 157
	i64 u0xf1c4b4005493d871, ; 511: System.Formats.Asn1.dll => 40
	i64 u0xf300e085f8acd238, ; 512: lib_System.ServiceProcess.dll.so => 137
	i64 u0xf34e52b26e7e059d, ; 513: System.Runtime.CompilerServices.VisualC.dll => 107
	i64 u0xf3ad9b8fb3eefd12, ; 514: lib_System.IO.UnmanagedMemoryStream.dll.so => 59
	i64 u0xf3ddfe05336abf29, ; 515: System => 170
	i64 u0xf408654b2a135055, ; 516: System.Reflection.Emit.ILGeneration.dll => 95
	i64 u0xf4103170a1de5bd0, ; 517: System.Linq.Queryable.dll => 64
	i64 u0xf42d20c23173d77c, ; 518: lib_System.ServiceModel.Web.dll.so => 136
	i64 u0xf4c1dd70a5496a17, ; 519: System.IO.Compression => 48
	i64 u0xf4ecf4b9afc64781, ; 520: System.ServiceProcess.dll => 137
	i64 u0xf518f63ead11fcd1, ; 521: System.Threading.Tasks => 150
	i64 u0xf5fc7602fe27b333, ; 522: System.Net.WebHeaderCollection => 82
	i64 u0xf70c0a7bf8ccf5af, ; 523: System.Web => 159
	i64 u0xf78774fb0d6d06e3, ; 524: merder.Core => 181
	i64 u0xf7e2cac4c45067b3, ; 525: lib_System.Numerics.Vectors.dll.so => 87
	i64 u0xf8aac5ea82de1348, ; 526: System.Linq.Queryable => 64
	i64 u0xf8b77539b362d3ba, ; 527: lib_System.Reflection.Primitives.dll.so => 100
	i64 u0xf915dc29808193a1, ; 528: System.Web.HttpUtility.dll => 158
	i64 u0xf9be54c8bcf8ff3b, ; 529: System.Security.AccessControl.dll => 122
	i64 u0xfa0e82300e67f913, ; 530: lib_System.AppContext.dll.so => 8
	i64 u0xfa2fdb27e8a2c8e8, ; 531: System.ComponentModel.EventBasedAsync => 17
	i64 u0xfa3f278f288b0e84, ; 532: lib_System.Net.Security.dll.so => 77
	i64 u0xfa645d91e9fc4cba, ; 533: System.Threading.Thread => 151
	i64 u0xfad4d2c770e827f9, ; 534: lib_System.IO.IsolatedStorage.dll.so => 54
	i64 u0xfb06dd2338e6f7c4, ; 535: System.Net.Ping.dll => 73
	i64 u0xfb087abe5365e3b7, ; 536: lib_System.Data.DataSetExtensions.dll.so => 25
	i64 u0xfb846e949baff5ea, ; 537: System.Xml.Serialization.dll => 163
	i64 u0xfbad3e4ce4b98145, ; 538: System.Security.Cryptography.X509Certificates => 130
	i64 u0xfbf0a31c9fc34bc4, ; 539: lib_System.Net.Http.dll.so => 68
	i64 u0xfc6b7527cc280b3f, ; 540: lib_System.Runtime.Serialization.Formatters.dll.so => 116
	i64 u0xfc93fc307d279893, ; 541: System.IO.Pipes.AccessControl.dll => 57
	i64 u0xfcd302092ada6328, ; 542: System.IO.MemoryMappedFiles.dll => 55
	i64 u0xfd49b3c1a76e2748, ; 543: System.Runtime.InteropServices.RuntimeInformation => 111
	i64 u0xfd536c702f64dc47, ; 544: System.Text.Encoding.Extensions => 139
	i64 u0xfda36abccf05cf5c, ; 545: System.Net.WebSockets.Client => 84
	i64 u0xff270a55858bac8d, ; 546: System.Security.Principal => 133
	i64 u0xff9b54613e0d2cc8, ; 547: System.Net.Http.Json => 67
	i64 u0xffdb7a971be4ec73 ; 548: System.ValueTuple.dll => 157
], align 8

@assembly_image_cache_indices = dso_local local_unnamed_addr constant [549 x i32] [
	i32 44, i32 15, i32 109, i32 176, i32 50, i32 9, i32 90, i32 74,
	i32 14, i32 106, i32 161, i32 21, i32 166, i32 172, i32 12, i32 100,
	i32 15, i32 12, i32 131, i32 99, i32 145, i32 41, i32 177, i32 7,
	i32 70, i32 134, i32 71, i32 69, i32 59, i32 54, i32 45, i32 129,
	i32 70, i32 85, i32 163, i32 96, i32 103, i32 146, i32 156, i32 167,
	i32 174, i32 85, i32 6, i32 7, i32 53, i32 105, i32 58, i32 124,
	i32 102, i32 173, i32 122, i32 23, i32 141, i32 101, i32 81, i32 2,
	i32 123, i32 10, i32 170, i32 73, i32 176, i32 150, i32 42, i32 49,
	i32 32, i32 149, i32 168, i32 30, i32 88, i32 81, i32 45, i32 1,
	i32 31, i32 44, i32 107, i32 121, i32 47, i32 95, i32 58, i32 153,
	i32 151, i32 104, i32 51, i32 22, i32 118, i32 98, i32 60, i32 85,
	i32 174, i32 28, i32 74, i32 72, i32 35, i32 16, i32 143, i32 40,
	i32 138, i32 96, i32 92, i32 154, i32 26, i32 142, i32 59, i32 144,
	i32 53, i32 31, i32 162, i32 36, i32 169, i32 54, i32 182, i32 94,
	i32 37, i32 162, i32 11, i32 80, i32 61, i32 57, i32 15, i32 113,
	i32 34, i32 108, i32 88, i32 96, i32 55, i32 100, i32 60, i32 11,
	i32 106, i32 71, i32 129, i32 120, i32 139, i32 130, i32 110, i32 135,
	i32 152, i32 161, i32 101, i32 26, i32 148, i32 5, i32 172, i32 104,
	i32 166, i32 103, i32 27, i32 97, i32 173, i32 177, i32 5, i32 3,
	i32 118, i32 35, i32 8, i32 161, i32 55, i32 180, i32 89, i32 46,
	i32 108, i32 49, i32 142, i32 67, i32 72, i32 84, i32 62, i32 93,
	i32 159, i32 137, i32 114, i32 61, i32 176, i32 138, i32 145, i32 42,
	i32 63, i32 83, i32 27, i32 38, i32 103, i32 74, i32 24, i32 125,
	i32 72, i32 111, i32 123, i32 121, i32 13, i32 4, i32 128, i32 119,
	i32 147, i32 43, i32 91, i32 179, i32 29, i32 153, i32 3, i32 46,
	i32 154, i32 20, i32 90, i32 43, i32 98, i32 30, i32 43, i32 82,
	i32 149, i32 112, i32 13, i32 109, i32 141, i32 18, i32 126, i32 69,
	i32 162, i32 24, i32 106, i32 66, i32 60, i32 114, i32 179, i32 2,
	i32 11, i32 124, i32 102, i32 109, i32 78, i32 115, i32 51, i32 61,
	i32 22, i32 75, i32 160, i32 41, i32 37, i32 40, i32 112, i32 23,
	i32 17, i32 83, i32 83, i32 157, i32 23, i32 52, i32 53, i32 98,
	i32 18, i32 127, i32 165, i32 47, i32 120, i32 66, i32 171, i32 16,
	i32 115, i32 63, i32 78, i32 125, i32 4, i32 8, i32 19, i32 81,
	i32 135, i32 87, i32 14, i32 36, i32 123, i32 178, i32 89, i32 20,
	i32 75, i32 99, i32 170, i32 86, i32 159, i32 38, i32 156, i32 149,
	i32 58, i32 117, i32 39, i32 119, i32 16, i32 181, i32 151, i32 45,
	i32 102, i32 173, i32 18, i32 50, i32 111, i32 101, i32 29, i32 132,
	i32 31, i32 132, i32 46, i32 154, i32 10, i32 136, i32 44, i32 35,
	i32 182, i32 48, i32 148, i32 180, i32 142, i32 65, i32 136, i32 50,
	i32 165, i32 48, i32 169, i32 178, i32 20, i32 10, i32 128, i32 62,
	i32 146, i32 155, i32 147, i32 130, i32 165, i32 167, i32 28, i32 86,
	i32 131, i32 105, i32 153, i32 56, i32 167, i32 172, i32 135, i32 39,
	i32 24, i32 116, i32 94, i32 52, i32 63, i32 126, i32 87, i32 131,
	i32 168, i32 0, i32 171, i32 6, i32 175, i32 4, i32 120, i32 21,
	i32 93, i32 68, i32 32, i32 62, i32 115, i32 34, i32 132, i32 164,
	i32 145, i32 158, i32 19, i32 79, i32 77, i32 17, i32 174, i32 0,
	i32 89, i32 128, i32 36, i32 122, i32 143, i32 126, i32 110, i32 56,
	i32 49, i32 30, i32 144, i32 150, i32 152, i32 37, i32 179, i32 79,
	i32 166, i32 3, i32 164, i32 14, i32 160, i32 156, i32 80, i32 107,
	i32 116, i32 68, i32 69, i32 47, i32 113, i32 9, i32 57, i32 67,
	i32 1, i32 22, i32 113, i32 105, i32 65, i32 147, i32 9, i32 175,
	i32 52, i32 119, i32 0, i32 146, i32 171, i32 84, i32 117, i32 19,
	i32 76, i32 93, i32 91, i32 124, i32 139, i32 158, i32 110, i32 13,
	i32 94, i32 33, i32 140, i32 42, i32 182, i32 143, i32 180, i32 27,
	i32 178, i32 76, i32 29, i32 70, i32 92, i32 99, i32 117, i32 33,
	i32 108, i32 39, i32 75, i32 112, i32 127, i32 91, i32 90, i32 97,
	i32 133, i32 2, i32 1, i32 168, i32 134, i32 12, i32 51, i32 95,
	i32 155, i32 65, i32 140, i32 155, i32 64, i32 121, i32 141, i32 88,
	i32 164, i32 181, i32 148, i32 86, i32 78, i32 73, i32 140, i32 129,
	i32 56, i32 114, i32 134, i32 92, i32 25, i32 77, i32 133, i32 33,
	i32 76, i32 163, i32 25, i32 6, i32 175, i32 127, i32 118, i32 177,
	i32 34, i32 5, i32 169, i32 32, i32 21, i32 97, i32 38, i32 7,
	i32 160, i32 80, i32 66, i32 152, i32 125, i32 138, i32 104, i32 41,
	i32 144, i32 71, i32 28, i32 79, i32 82, i32 26, i32 157, i32 40,
	i32 137, i32 107, i32 59, i32 170, i32 95, i32 64, i32 136, i32 48,
	i32 137, i32 150, i32 82, i32 159, i32 181, i32 87, i32 64, i32 100,
	i32 158, i32 122, i32 8, i32 17, i32 77, i32 151, i32 54, i32 73,
	i32 25, i32 163, i32 130, i32 68, i32 116, i32 57, i32 55, i32 111,
	i32 139, i32 84, i32 133, i32 67, i32 157
], align 4

@marshal_methods_number_of_classes = dso_local local_unnamed_addr constant i32 0, align 4

@marshal_methods_class_cache = dso_local local_unnamed_addr global [0 x %struct.MarshalMethodsManagedClass] zeroinitializer, align 8

; Names of classes in which marshal methods reside
@mm_class_names = dso_local local_unnamed_addr constant [0 x ptr] zeroinitializer, align 8

@mm_method_names = dso_local local_unnamed_addr constant [1 x %struct.MarshalMethodName] [
	%struct.MarshalMethodName {
		i64 u0x0000000000000000, ; name: 
		ptr @.MarshalMethodName.0_name; char* name
	} ; 0
], align 8

; get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr)
@get_function_pointer = internal dso_local unnamed_addr global ptr null, align 8

; Functions

; Function attributes: memory(write, argmem: none, inaccessiblemem: none) "min-legal-vector-width"="0" mustprogress "no-trapping-math"="true" nofree norecurse nosync nounwind "stack-protector-buffer-size"="8" uwtable willreturn
define void @xamarin_app_init(ptr nocapture noundef readnone %env, ptr noundef %fn) local_unnamed_addr #0
{
	%fnIsNull = icmp eq ptr %fn, null
	br i1 %fnIsNull, label %1, label %2

1: ; preds = %0
	%putsResult = call noundef i32 @puts(ptr @.mm.0)
	call void @abort()
	unreachable 

2: ; preds = %1, %0
	store ptr %fn, ptr @get_function_pointer, align 8, !tbaa !3
	ret void
}

; Strings
@.mm.0 = private unnamed_addr constant [40 x i8] c"get_function_pointer MUST be specified\0A\00", align 1

;MarshalMethodName
@.MarshalMethodName.0_name = private unnamed_addr constant [1 x i8] c"\00", align 1

; External functions

; Function attributes: "no-trapping-math"="true" noreturn nounwind "stack-protector-buffer-size"="8"
declare void @abort() local_unnamed_addr #2

; Function attributes: nofree nounwind
declare noundef i32 @puts(ptr noundef) local_unnamed_addr #1
attributes #0 = { memory(write, argmem: none, inaccessiblemem: none) "min-legal-vector-width"="0" mustprogress "no-trapping-math"="true" nofree norecurse nosync nounwind "stack-protector-buffer-size"="8" "target-cpu"="generic" "target-features"="+fix-cortex-a53-835769,+neon,+outline-atomics,+v8a" uwtable willreturn }
attributes #1 = { nofree nounwind }
attributes #2 = { "no-trapping-math"="true" noreturn nounwind "stack-protector-buffer-size"="8" "target-cpu"="generic" "target-features"="+fix-cortex-a53-835769,+neon,+outline-atomics,+v8a" }

; Metadata
!llvm.module.flags = !{!0, !1, !7, !8, !9, !10}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!llvm.ident = !{!2}
!2 = !{!".NET for Android remotes/origin/release/10.0.1xx @ e1d3646df9cb50b2a0924f5b67fa78f9750ae489"}
!3 = !{!4, !4, i64 0}
!4 = !{!"any pointer", !5, i64 0}
!5 = !{!"omnipotent char", !6, i64 0}
!6 = !{!"Simple C++ TBAA"}
!7 = !{i32 1, !"branch-target-enforcement", i32 0}
!8 = !{i32 1, !"sign-return-address", i32 0}
!9 = !{i32 1, !"sign-return-address-all", i32 0}
!10 = !{i32 1, !"sign-return-address-with-bkey", i32 0}
