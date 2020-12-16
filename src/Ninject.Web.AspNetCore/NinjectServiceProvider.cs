﻿using Microsoft.Extensions.DependencyInjection;
using Ninject.Planning.Bindings;
using System;

namespace Ninject.Web.AspNetCore
{
	/// <summary>
	/// We have to wrap IKernel here as an IServiceProvider,
	/// although Ninject would itself implement this interface (but with a wrong semantic for the not found case) 
	/// and additionally we want to implement the optional ISupportRequiredService.
	/// 
	/// Note: ASP.NET Core wants to use a method from ISupportRequiredService to resolve a non-optional service.
	/// Although it's implemented on Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions in a generic way
	/// we implement it here to have the nicer exceptions from NInject so that it's possible to distinguish the "not registered at all"
	/// vs the "ambigious matches found" cases.
	/// </summary>
	public class NinjectServiceProvider : IServiceProvider, ISupportRequiredService
	{
		private readonly IKernel _kernel;

		public NinjectServiceProvider(IKernel kernel)
		{
			_kernel = kernel;
		}

		public object GetRequiredService(Type serviceType)
		{
			var result = _kernel.Get(serviceType, LatestBindingConstraint);
			return result;
		}

		public object GetService(Type serviceType)
		{
			// Call TryGet as IServiceProvider.GetService must return null if not found.
			// See also https://github.com/ninject/Ninject/issues/378 and NinjectBugsRegressionTest
			try
			{
				var result = _kernel.TryGet(serviceType, LatestBindingConstraint);
				return result;
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		private bool LatestBindingConstraint(IBindingMetadata bindingMetadata)
		{
			return bindingMetadata.Get<BindingIndex.Item>(nameof(BindingIndex))?.IsLatest ?? true;
		}
	}
}
