using System.IO;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class CustomRateLimitMiddleware : RateLimitMiddleware<CustomIpRateLimitProcessor>
    {
        private readonly ILogger _logger;
        private readonly IRateLimitConfiguration _config;

        public CustomRateLimitMiddleware(RequestDelegate next,
            IProcessingStrategy processingStrategy,
            IOptions<IpRateLimitOptions> options,
            IIpPolicyStore policyStore,
            IRateLimitConfiguration config)
            : base(next, options?.Value, new CustomIpRateLimitProcessor(options?.Value!, policyStore, processingStrategy), config)
        {
            _config = config;
            _logger = Log.Logger.ForContext<CustomRateLimitMiddleware>();
        }

        protected override void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity, RateLimitCounter counter, RateLimitRule rule)
        {
            _logger.Information($"[IP-RATE-LIMITER] Request {identity.HttpVerb}:{identity.Path} from IP {identity.ClientIp} has been blocked, " +
                                $"quota {rule.Limit}/{rule.Period} exceeded by {counter.Count - rule.Limit}. Blocked by rule {rule.Endpoint}, " +
                                $"TraceIdentifier {httpContext.TraceIdentifier}. MonitorMode: {rule.MonitorMode}");
        }

        public override async Task<ClientRequestIdentity> ResolveIdentityAsync(HttpContext httpContext)
        {
            var identity = await base.ResolveIdentityAsync(httpContext);

            if (httpContext.Request.Protocol == "HTTP/2")
            {
                identity.ClientIp = identity.ClientIp + "/" + httpContext.Connection.RemotePort;
                return identity;
            }

            if (httpContext.Request.Protocol == "HTTP/1.1")
            {
                identity.ClientIp = identity.ClientIp + "/" + httpContext.Connection.RemotePort;
                httpContext.Request.EnableBuffering();
                var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                httpContext.Request.Body.Seek(0, SeekOrigin.Begin);
                if (body.Contains("stageTransaction"))
                {
                    identity.Path = "/graphql/stagetransaction";
                }

                return identity;
            }

            return identity;
        }
    }
}
