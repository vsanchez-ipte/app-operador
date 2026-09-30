using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.UnitTests.Application;

public sealed class CodigosErrorJacobTests
{
	[Theory]
	[InlineData("appincidencias.catalogo.invalido")]
	[InlineData("appincidencias.nota.requerida")]
	[InlineData("appincidencias.km.fueradecorredor")]
	[InlineData("appincidencias.validacion.campo")]
	[InlineData("appincidencias.permiso.revocado")]
	[InlineData("appincidencias.operador.ajeno")]
	[InlineData("appevidencias.formato.nopermitido")]
	[InlineData("appevidencias.archivo.demasiadogrande")]
	[InlineData("appevidencias.archivos.demasiados")]
	[InlineData("appevidencias.incidencia.noexiste")]
	public void LosDiezCodigosDelContratoSonFuncionales(string codigo)
	{
		Assert.Equal(FamiliaErrorSincronizacion.Funcional, CodigosErrorJacob.FamiliaDe(codigo));
	}

	[Theory]
	[InlineData("appincidencias.error.tecnico")]
	[InlineData("appevidencias.error.tecnico")]
	public void LosCodigosDeFalloDelServidorSonTecnicos(string codigo)
	{
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, CodigosErrorJacob.FamiliaDe(codigo));
	}

	[Fact]
	public void LaPlazaNoResueltaEsTecnicaAunqueSuenaADatoMalCapturado()
	{
		Assert.Equal(
			FamiliaErrorSincronizacion.Tecnico,
			CodigosErrorJacob.FamiliaDe(CodigosErrorJacob.PlazaNoResuelta));
	}

	[Fact]
	public void UnCodigoDesconocidoSeTrataComoTecnico()
	{
		Assert.Equal(
			FamiliaErrorSincronizacion.Tecnico,
			CodigosErrorJacob.FamiliaDe("appincidencias.algo.que.no.existia"));
	}

	[Fact]
	public void SinCodigoSeTrataComoTecnico()
	{
		// Es el caso de la red caída y del tiempo agotado: no hay respuesta que traiga código.
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, CodigosErrorJacob.FamiliaDe(null));
	}

	[Fact]
	public void ElCasoDeLasLetrasNoCambiaLaDecision()
	{
		// El contrato dice minúsculas, pero hacer depender de eso el reintento sería frágil.
		Assert.True(CodigosErrorJacob.EsFuncional("APPINCIDENCIAS.NOTA.REQUERIDA"));
	}
}
