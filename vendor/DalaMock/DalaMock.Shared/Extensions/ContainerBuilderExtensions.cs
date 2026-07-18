namespace DalaMock.Shared.Extensions;

using System;
using System.Reflection;

using Autofac;
using Autofac.Builder;
using Autofac.Features.Scanning;

public static class ContainerBuilderExtensions
{
    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingletonSelfAndInterfaces<T>(this ContainerBuilder builder, params Type[] extraTypes)
        where T : notnull
    {
        return builder.RegisterType<T>().AsSelf().As(extraTypes).AsImplementedInterfaces().SingleInstance();
    }

    public static IRegistrationBuilder<object, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingletonSelfAndInterfaces(this ContainerBuilder builder, Type type, params Type[] extraTypes)
    {
        return builder.RegisterType(type).AsSelf().As(extraTypes).AsImplementedInterfaces().SingleInstance();
    }

    public static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> RegisterSingletonsSelfAndInterfaces<T>(this ContainerBuilder builder, Assembly executingAssembly, params Type[] extraTypes)
        where T : notnull
    {
        return builder.RegisterAssemblyTypes(executingAssembly)
               .AssignableTo<T>()
               .As<T>()
               .AsSelf()
               .As(extraTypes)
               .SingleInstance();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterTransientSelfAndInterfaces<T>(this ContainerBuilder builder, params Type[] extraTypes)
        where T : notnull
    {
        return builder.RegisterType<T>().AsSelf().As(extraTypes).AsImplementedInterfaces().InstancePerDependency();
    }

    public static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> RegisterTransientsSelfAndInterfaces<T>(this ContainerBuilder builder, Assembly executingAssembly, params Type[] extraTypes)
        where T : notnull
    {
        return builder.RegisterAssemblyTypes(executingAssembly)
               .AssignableTo<T>()
               .As<T>()
               .As(extraTypes)
               .AsSelf()
               .InstancePerDependency();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterExternalTransientSelfAndInterfaces<T>(this ContainerBuilder builder, params Type[] extraTypes)
        where T : notnull
    {
        return builder.RegisterType<T>().AsSelf().As(extraTypes).AsImplementedInterfaces().InstancePerDependency().ExternallyOwned();
    }

    public static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> RegisterExternalTransientsSelfAndInterfaces<T>(this ContainerBuilder builder, Assembly executingAssembly, params Type[] extraTypes)
        where T : notnull
    {
        return builder.RegisterAssemblyTypes(executingAssembly)
                      .AssignableTo<T>()
                      .As<T>()
                      .As(extraTypes)
                      .AsSelf()
                      .AsImplementedInterfaces()
                      .InstancePerDependency()
                      .ExternallyOwned();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingletonSelf<T>(this ContainerBuilder builder)
        where T : notnull
    {
        return builder.RegisterType<T>()
                      .As<T>()
                      .AsSelf()
                      .SingleInstance();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterTransientSelf<T>(this ContainerBuilder builder)
        where T : notnull
    {
        return builder.RegisterType<T>()
                      .As<T>()
                      .AsSelf();
    }
}
