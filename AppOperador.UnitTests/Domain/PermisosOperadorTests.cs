using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// Permisos que solo puede conceder Jacob CCO (JTT-1379 CA 7 y CA 8).
/// </summary>
public class PermisosOperadorTests
{
	private const string Modulo = "APP_OPERADOR_MOVIL";

	// ---------- Adopción de la lista del servidor ----------

	[Fact]
	public void Sin_lista_no_hay_ningun_permiso()
	{
		Assert.Empty(PermisosOperador.DelServidor(null));
	}

	[Fact]
	public void Una_lista_vacia_no_concede_nada()
	{
		Assert.Empty(PermisosOperador.DelServidor([]));
	}

	[Fact]
	public void Recorta_los_espacios_de_cada_permiso()
	{
		var permisos = PermisosOperador.DelServidor(["  CAPTURA  "]);

		Assert.Equal(["CAPTURA"], permisos);
	}

	[Fact]
	public void Descarta_las_entradas_vacias()
	{
		var permisos = PermisosOperador.DelServidor(["CAPTURA", "", "   "]);

		Assert.Equal(["CAPTURA"], permisos);
	}

	[Fact]
	public void Elimina_repetidos_sin_distinguir_mayusculas()
	{
		var permisos = PermisosOperador.DelServidor(["CAPTURA", "captura", "Captura"]);

		Assert.Single(permisos);
	}

	[Fact]
	public void Ordena_para_que_el_orden_de_llegada_no_importe()
	{
		var uno = PermisosOperador.DelServidor(["SYNC", "CAPTURA"]);
		var otro = PermisosOperador.DelServidor(["CAPTURA", "SYNC"]);

		Assert.Equal(uno.ToArray(), otro.ToArray());
	}

	[Fact]
	public void No_inventa_permisos_que_el_servidor_no_mando()
	{
		var permisos = PermisosOperador.DelServidor(["CAPTURA"]);

		Assert.False(permisos.Contiene("SYNC"));
	}

	// ---------- CA 7: no se puede modificar desde la app ----------

	[Fact]
	public void Copia_la_lista_y_no_refleja_cambios_posteriores()
	{
		// El fallo real que esto cierra: con IReadOnlyList se conservaba la lista de quien
		// llamaba, así que mutarla después cambiaba los permisos de la sesión ya abierta.
		var original = new List<string> { "CAPTURA" };
		var permisos = PermisosOperador.DelServidor(original);

		original.Add("ADMINISTRAR");

		Assert.Single(permisos);
		Assert.False(permisos.Contiene("ADMINISTRAR"));
	}

	[Fact]
	public void No_expone_ninguna_coleccion_modificable()
	{
		// Sin esto, un cast bastaba para agregarse un permiso.
		var permisos = PermisosOperador.DelServidor([Modulo]);

		Assert.IsNotAssignableFrom<ICollection<string>>(permisos);
		Assert.IsNotAssignableFrom<IList<string>>(permisos);
	}

	// ---------- Consulta ----------

	[Fact]
	public void Contiene_no_distingue_mayusculas_ni_espacios()
	{
		var permisos = PermisosOperador.DelServidor([Modulo]);

		Assert.True(permisos.Contiene(Modulo));
		Assert.True(permisos.Contiene(" app_operador_movil "));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Contiene_es_falso_para_un_permiso_sin_nombre(string? permiso)
	{
		Assert.False(PermisosOperador.DelServidor([Modulo]).Contiene(permiso!));
	}

	// ---------- CA 8: respaldo del token ----------

	[Fact]
	public void Estan_respaldados_si_el_token_trae_los_mismos_modulos()
	{
		Assert.True(PermisosOperador.DelServidor([Modulo]).RespaldadosPor([Modulo]));
	}

	[Fact]
	public void Estan_respaldados_si_el_token_trae_ademas_otros_modulos()
	{
		// Que el token conceda de más no es problema de la app: la app solo usa lo que Jacob
		// puso en el cuerpo, y aquí se comprueba que no exceda al token.
		var permisos = PermisosOperador.DelServidor([Modulo]);

		Assert.True(permisos.RespaldadosPor([Modulo, "OTRO_MODULO"]));
	}

	[Fact]
	public void El_respaldo_no_distingue_mayusculas()
	{
		Assert.True(PermisosOperador.DelServidor([Modulo]).RespaldadosPor(["app_operador_movil"]));
	}

	[Fact]
	public void No_estan_respaldados_si_al_token_le_falta_uno()
	{
		// El caso que importa: un cuerpo alterado que concede más de lo que el token firma.
		var permisos = PermisosOperador.DelServidor([Modulo, "ADMINISTRAR"]);

		Assert.False(permisos.RespaldadosPor([Modulo]));
	}

	[Fact]
	public void No_estan_respaldados_si_el_token_no_declara_modulos()
	{
		Assert.False(PermisosOperador.DelServidor([Modulo]).RespaldadosPor([]));
	}

	[Fact]
	public void No_estan_respaldados_si_no_se_pudo_leer_el_token()
	{
		// Un token ilegible no respalda nada. Bloquear es la salida correcta.
		Assert.False(PermisosOperador.DelServidor([Modulo]).RespaldadosPor(null));
	}

	[Fact]
	public void Una_lista_vacia_esta_respaldada_por_cualquier_token()
	{
		// No concede nada, así que no hay nada que respaldar.
		Assert.True(PermisosOperador.Ninguno.RespaldadosPor(null));
		Assert.True(PermisosOperador.Ninguno.RespaldadosPor([]));
	}
}
