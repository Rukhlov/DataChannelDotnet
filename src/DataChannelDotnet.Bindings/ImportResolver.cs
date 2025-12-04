using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DataChannelDotnet.Bindings
{
	public static class ImportResolver
	{
		private const string LibraryName = "datachannel";

		internal static void Init()
		{
			var assemblyDir = Path.GetDirectoryName(typeof(ImportResolver).Assembly.Location) ?? AppContext.BaseDirectory;

			string fileName = null;
			string rid = null;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				fileName = "datachannel.dll";
				rid = Environment.Is64BitProcess ? "win-x64" : "win-x86";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				fileName = "datachannel.so";
				rid = "linux-x64";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				fileName = "datachannel.dylib";
				rid = "osx-x64";
			}

			if (rid != null && fileName != null)
			{
				string runtimeSpecificPath = Path.Combine(assemblyDir, "runtimes", rid, "native", fileName);

				if (File.Exists(runtimeSpecificPath))
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						string nativeDir = Path.GetDirectoryName(runtimeSpecificPath);
						if (nativeDir != null)
						{
							SetDllDirectory(nativeDir);
						}
					}
					// For Linux/OSX, we rely on the library being in the correct path
					// The DllImport will find it automatically if it's in the same directory
					// or in a standard library path
				}
			}
		}

		//This is here because we need to decide which native libraries to load if the user has not specified
		//an RID. If this RID is set, the native libraries will be copied to the root of the build directory, otherwise
		//we need to figure out which file to load.

		//It seems like ubuntu (at least of the github actions runner) uses an ubuntu specific RID instead of linux-x64.

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetDllDirectory(string lpPathName);
	}
}