using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace TattooFlash.Web.Configuration;

public class ForwardedHeadersConfiguration : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.30.10.0/24"));
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.30.12.0/24"));
    }
}
