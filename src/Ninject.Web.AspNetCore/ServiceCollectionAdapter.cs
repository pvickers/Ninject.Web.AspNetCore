﻿using Microsoft.Extensions.DependencyInjection;
using Ninject.Syntax;
using Ninject.Web.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ninject.Web.AspNetCore
{
	public class ServiceCollectionAdapter
	{
		private readonly IDictionary<Type, BindingIndex> _bindingIndexMap = new Dictionary<Type, BindingIndex>();

		public void Populate(IKernel kernel, IServiceCollection serviceCollection)
		{
			if (serviceCollection == null)
			{
				throw new ArgumentNullException(nameof(serviceCollection));
			}

			var adapters = kernel.GetAll<IPopulateAdapter>().ToList();
			var bindingIndex = new BindingIndex();

			foreach (var descriptor in serviceCollection)
			{
				if (UsedCustomBinding(kernel, adapters, descriptor))
				{
					continue;
				}

				ConfigureImplementationAndLifecycle(kernel.Bind(descriptor.ServiceType), descriptor, bindingIndex);
			}

			foreach (var adapter in adapters)
			{
				adapter.AdaptAfterPopulate(kernel);
			}
		}

		private bool UsedCustomBinding(IKernel kernel, List<IPopulateAdapter> adapters, ServiceDescriptor descriptor)
		{
			foreach (var adapter in adapters)
			{
				if (adapter.AdaptDescriptor(kernel, descriptor))
				{
					return true;
				}
			}

			return false;
		}

		private IBindingWithOrOnSyntax<T> ConfigureImplementationAndLifecycle<T>(
			IBindingToSyntax<T> bindingToSyntax,
			ServiceDescriptor descriptor,
			BindingIndex bindingIndex) where T : class
		{
			IBindingNamedWithOrOnSyntax<T> result;
			if (descriptor.ImplementationType != null)
			{
				result = ConfigureLifecycle(bindingToSyntax.To(descriptor.ImplementationType), descriptor.Lifetime);
			}
			else if (descriptor.ImplementationFactory != null)
			{

				result = ConfigureLifecycle(bindingToSyntax.ToMethod(context
					=>
				{
					var provider = context.Kernel.Get<IServiceProvider>();
					return descriptor.ImplementationFactory(provider) as T;
				}), descriptor.Lifetime);
			}
			else
			{
				// use ToMethod here as ToConstant has the wrong return type.
				result = bindingToSyntax.ToMethod(context => descriptor.ImplementationInstance as T).InSingletonScope();
			}

			return result
				.WithMetadata(nameof(ServiceDescriptor), descriptor)
				.WithMetadata(nameof(BindingIndex), bindingIndex.Next(descriptor.ServiceType));
		}

		private IBindingNamedWithOrOnSyntax<T> ConfigureLifecycle<T>(
			IBindingInSyntax<T> bindingInSyntax,
			ServiceLifetime lifecycleKind)
		{
			switch (lifecycleKind)
			{
				case ServiceLifetime.Singleton:
					// Microsoft.Extensions.DependencyInjection expects its singletons to be disposed when the root service scope
					// and/or the root IServiceProvider is disposed.
					return bindingInSyntax.InScope(context => {
						return (context.Kernel as AspNetCoreKernel).RootScope;
					});
				case ServiceLifetime.Scoped:
					return bindingInSyntax.InRequestScope();
				case ServiceLifetime.Transient:
					// Microsoft.Extensions.DependencyInjection expects transient services to be disposed when the IServiceScope
					// in which they were created is disposed. See the compliance tests for more details.
					return bindingInSyntax.InScope(context => {
						var scope = context.Parameters.OfType<ServiceProviderScopeParameter>().SingleOrDefault();
						return scope?.DeriveTransientScope();
					});
				default:
					throw new NotSupportedException();
			}
		}
	}
}
