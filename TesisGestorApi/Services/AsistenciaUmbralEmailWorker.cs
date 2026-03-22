using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class AsistenciaUmbralEmailWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AsistenciaUmbralEmailWorker> _logger;

        public AsistenciaUmbralEmailWorker(IServiceScopeFactory scopeFactory, ILogger<AsistenciaUmbralEmailWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IAsistenciaUmbralService>();
                    await svc.EnviarPendientesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en AsistenciaUmbralEmailWorker");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}