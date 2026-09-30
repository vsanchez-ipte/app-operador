using AppOperador.Aplicacion.Modelos;

namespace AppOperador.UnitTests.Application;

public class ReglaEstadoComunicacionTests
{
	[Fact]
	public void Con_enlace_esta_en_linea()
	{
		var estado = ReglaEstadoComunicacion.Determinar(
			hayEnlace: true, comprobando: false, CausaSinEnlace.Ninguna);

		Assert.Equal(EstadoComunicacion.EnLinea, estado);
	}

	[Fact]
	public void Sin_enlace_y_sin_respuesta_del_servidor_esta_sin_conexion()
	{
		var estado = ReglaEstadoComunicacion.Determinar(
			hayEnlace: false, comprobando: false, CausaSinEnlace.SinTransporte);

		Assert.Equal(EstadoComunicacion.SinConexion, estado);
	}

	[Fact]
	public void Si_el_servidor_contesto_con_error_es_error_de_servicio()
	{
		var estado = ReglaEstadoComunicacion.Determinar(
			hayEnlace: false, comprobando: false, CausaSinEnlace.RespuestaDeError);

		Assert.Equal(EstadoComunicacion.ErrorDeServicio, estado);
	}

	[Theory]
	[InlineData(true, CausaSinEnlace.Ninguna)]
	[InlineData(false, CausaSinEnlace.SinTransporte)]
	[InlineData(false, CausaSinEnlace.RespuestaDeError)]
	public void Mientras_comprueba_manda_revalidando(bool hayEnlace, CausaSinEnlace causa)
	{
		var estado = ReglaEstadoComunicacion.Determinar(hayEnlace, comprobando: true, causa);

		Assert.Equal(EstadoComunicacion.Revalidando, estado);
	}

	[Fact]
	public void Con_enlace_la_causa_anterior_ya_no_cuenta()
	{
		var estado = ReglaEstadoComunicacion.Determinar(
			hayEnlace: true, comprobando: false, CausaSinEnlace.RespuestaDeError);

		Assert.Equal(EstadoComunicacion.EnLinea, estado);
	}

	[Theory]
	[InlineData(CausaSinEnlace.Ninguna)]
	[InlineData(CausaSinEnlace.SinTransporte)]
	[InlineData(CausaSinEnlace.SesionRechazada)]
	[InlineData(CausaSinEnlace.RespuestaDeError)]
	[InlineData(CausaSinEnlace.SinSesion)]
	public void Sin_enlace_nunca_se_muestra_en_linea(CausaSinEnlace causa)
	{
		var estado = ReglaEstadoComunicacion.Determinar(
			hayEnlace: false, comprobando: false, causa);

		Assert.NotEqual(EstadoComunicacion.EnLinea, estado);
	}
}
