using System.Reflection;
using ActualChat.Testing.Host;
using RestEase;

namespace ActualChat.Chat.IntegrationTests;

public class ClientDefsTest : AppHostTestBase
{
    public ClientDefsTest(ITestOutputHelper @out) : base(@out)
    { }

    [Fact]
    public async Task ShouldHaveCorrectClientDefs()
    {
        IServiceCollection services = null!;
        using var _ = await NewAppHost(configureServices: sc => services = sc);

        new ClientDefsValidator().Validate(services);
    }

    // TODO: Move it into App.Server when it's fast enough
    private class ClientDefsValidator
    {
        public void Validate(IServiceCollection serviceCollection)
        {
            var computeServiceDescriptors = serviceCollection
                .Where(x => x.ServiceType.IsInterface && x.ServiceType.IsAssignableTo(typeof(IComputeService)))
                .Where(x => x.ServiceType.Namespace!.OrdinalStartsWith("ActualChat."))
                .Where(x => !x.ServiceType.Name.OrdinalEndsWith("Backend"))
                .Select(x => x.ServiceType)
                .ToList();
            var clientDefMap = GetClientDefMap();
            foreach (var computeService in computeServiceDescriptors) {
                var clientDef = clientDefMap.GetValueOrDefault(computeService.Name + "ClientDef")
                    ?? throw new Exception($"{computeService} does not have client def.");

                foreach (var method in GetComputeServiceMethods(computeService)) {
                    var clientDefMethod = clientDef.GetMethod(method.Name)
                        ?? throw new Exception($"{clientDef}.{method.Name} is missing");

                    if (method.GetParameters().Length != clientDefMethod.GetParameters().Length)
                        throw new Exception($"{clientDef}.{clientDefMethod.Name} parameters count does not match {computeService}.{method.Name}.");

                    foreach (var (parameter, clientDefParameter) in method.GetParameters()
                                 .Zip(clientDefMethod.GetParameters())) {
                        if (!OrdinalEquals(parameter.Name, clientDefParameter.Name))
                            throw new Exception($"Parameter '{parameter}' of {clientDef}.{clientDefMethod.Name} does not match {clientDefParameter}.");

                        if (IsCommandHandler(method)) {
                            var postAttribute = clientDefMethod.GetCustomAttribute<PostAttribute>()
                                ?? throw new Exception($"{clientDef}.{clientDefMethod.Name} does not have PostAttribute.");
                            if (!OrdinalEquals(postAttribute.Path, clientDefMethod.Name))
                                throw new Exception($"{clientDef}.{clientDefMethod.Name}: Path of PostAttribute does not match method name.");
                        }
                        else if (IsComputeMethod(method)) {
                            var getAttribute = clientDefMethod.GetCustomAttribute<GetAttribute>()
                                ?? throw new Exception($"{clientDef}.{clientDefMethod.Name} does not have GetAttribute");
                            if (!OrdinalEquals(getAttribute.Path, clientDefMethod.Name))
                                throw new Exception($"{clientDef}.{clientDefMethod.Name}: GetAttribute path does not match method name.");
                        }

                        if (clientDefParameter.ParameterType.IsAssignableTo(typeof(ICommand)))
                            if (clientDefParameter.GetCustomAttribute<BodyAttribute>() == null)
                                throw new Exception($"Parameter {clientDefParameter.Name} of {clientDef}.{clientDefMethod.Name} does not have BodyAttribute.");
                    }

                    if (clientDefMethod.ReturnType != method.ReturnType) {
                        throw new Exception($"Return type 'clientDefMethod.ReturnType' of {clientDef}.{clientDefMethod.Name} does not match return type '{method.ReturnType}' of {computeService}.{method.Name}.");
                    }
                }
            }
        }

        private List<MethodInfo> GetComputeServiceMethods(Type computeService)
            => computeService.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => IsCommandHandler(x) || IsComputeMethod(x))
                .ToList();

        private static bool IsCommandHandler(MethodInfo x) => x.GetCustomAttribute<CommandHandlerAttribute>() != null;
        private static bool IsComputeMethod(MethodInfo x) => x.GetCustomAttribute<ComputeMethodAttribute>() != null;

        private static Dictionary<string, Type> GetClientDefMap()
        {
            var clientAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.GetName().Name.OrdinalStartsWith("ActualChat")
                    && x.GetName().Name.OrdinalEndsWith(".Client"))
                .ToList();
 #pragma warning disable IL2026
            var clientDefMap = clientAssemblies.SelectMany(x => x.GetTypes())
 #pragma warning restore IL2026
                .Where(x => x.IsInterface && x.Name.OrdinalEndsWith("ClientDef"))
                .ToDictionary(x => x.Name, StringComparer.Ordinal);
            return clientDefMap;
        }
    }
}
