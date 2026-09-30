using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.UnitTests.Application;

public sealed class EvidenciaPendienteTests
{
	private static EvidenciaPendiente Pendiente(EstadoSincronizacion estado, string? error = null) =>
		new("ev-1", "foto.jpg", "image/jpeg", 1024, estado, "LOC-673529", error);

	[Fact]
	public void UnaQueNoSeHaIntentado_estaPendienteDeEnviar()
	{
		Assert.Equal("pendiente de enviar", Pendiente(EstadoSincronizacion.Pendiente).EstadoLegible);
	}

	[Fact]
	public void UnFalloTecnico_seReintentara()
	{
		var pendiente = Pendiente(EstadoSincronizacion.Fallido, CodigosErrorJacob.ErrorTecnico);

		Assert.Equal("con error, se reintentará", pendiente.EstadoLegible);
	}

	[Fact]
	public void UnRechazoDelCco_noPrometeReintento()
	{
		var pendiente = Pendiente(EstadoSincronizacion.Fallido, "appevidencias.formato.nopermitido");

		Assert.Equal("rechazada por el CCO, no se enviará", pendiente.EstadoLegible);
	}
}
