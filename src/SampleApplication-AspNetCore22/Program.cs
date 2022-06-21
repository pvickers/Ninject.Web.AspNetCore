﻿using Microsoft.AspNetCore.Hosting;
using Ninject;
using Ninject.Web.AspNetCore.Hosting;
using Ninject.Web.Common.SelfHost;
using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server;

namespace SampleApplication_AspNetCore
{
	public class Program
	{
		public static void Main(string[] args)
		{
			// Simple (and probably unreliable) IIS detection mechanism
			var model = Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES") == "Microsoft.AspNetCore.Server.IISIntegration" ? "IIS" : null;
			// The hosting model can be explicitly configured with the SERVER_HOSTING_MODEL environment variable.
			// See https://www.andrecarlucci.com/en/setting-environment-variables-for-asp-net-core-when-publishing-on-iis/ for
			// setting the variable in IIS.
			model = Environment.GetEnvironmentVariable("SERVER_HOSTING_MODEL") ?? model;
			// Command line arguments have higher precedence than environment variables
			model = args.FirstOrDefault(arg => arg.StartsWith("--use"))?.Substring(5) ?? model;

			var hostConfiguration = new AspNetCoreHostConfiguration(args)
					.UseStartup<Startup>()
					.UseWebHostBuilder(CreateWebHostBuilder)
					.BlockOnStart();

			switch (model)
			{
				case "Kestrel":
					hostConfiguration.UseKestrel();
					break;

				case "HttpSys":
					hostConfiguration.UseHttpSys();
					break;

				case "IIS":
					hostConfiguration.UseIIS();
					hostConfiguration.UseKestrel();
					break;

				case "IISExpress":
					// Yes, _this_ is actually a "thing"...
					// The netstandard2.0 version of ASP.NET Core 2.2 works quite different when it comes to IISExpress integration
					// than its .NET Core counterpart.
					// * In a .NET 4.8 project, you MUST to UseKestrel when running in IISExpress
					// * In a .NET Core project (tested in 2.2 and 3.1), you MUST to UseIIS when running in IISExpress
					//
					// ... This just makes my brain hurt.
					if (IsDotNetCoreRuntime())
					{
						hostConfiguration.UseIIS();
					}
					else
					{
						hostConfiguration.UseKestrel();
					}
					break;

				default:
					throw new ArgumentException($"Unknown hosting model '{model}'");
			}

			var host = new NinjectSelfHostBootstrapper(CreateKernel, hostConfiguration);
			host.Start();
		}

		public static IKernel CreateKernel()
		{
			var settings = new NinjectSettings();
			// Unfortunately, in .NET Core projects, referenced NuGet assemblies are not copied to the output directory
			// in a normal build which means that the automatic extension loading does not work _reliably_ and it is
			// much more reasonable to not rely on that and load everything explicitly.
			settings.LoadExtensions = false;

			var kernel = new StandardKernel(settings);

			kernel.Load(typeof(AspNetCoreHostConfiguration).Assembly);

			return kernel;
		}

		public static IWebHostBuilder CreateWebHostBuilder()
		{
			return new DefaultWebHostConfiguration(null)
				.ConfigureAll()
				.GetBuilder()
				.UseWebRoot(@"..\SampleApplication-Shared\wwwroot");
		}

		public static bool IsDotNetCoreRuntime()
		{
			return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.Contains(".NET Core");
		}
	}
}
