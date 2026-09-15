using System.Diagnostics;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>Corregir y reenviar un registro que el CCO rechazó (JTT-291 CA 8).</summary>
public sealed partial class CapturaViewModel
{
	// Nombre del parámetro con el que la Cola manda a corregir un rechazado (JTT-291 CA 8).
	public const string ParametroCorregir = "corregir";

	// Clave que llegó por navegación desde la Cola (JTT-291 CA 8). Se guarda hasta que la
	// pantalla termine de inicializarse: abrir el registro antes dejaría que la ubicación
	// recalculada pisara el kilómetro que se acaba de reponer.
	private string? _claveACorregir;

	/// <summary>
	/// Clave local del registro rechazado que se está corrigiendo, o <see langword="null"/>
	/// (JTT-291 CA 8).
	/// </summary>
	/// <remarks>
	/// Es un modo distinto de editar un borrador y no se mezclan: un rechazado ya estuvo en la
	/// cola, no se puede «guardar como borrador» ni «eliminar», y el botón principal no lo
	/// convierte sino que lo reenvía. Si hay un rechazado abierto no hay borrador abierto.
	/// </remarks>
	[ObservableProperty]
	public partial string? RechazadaEnCorreccion { get; set; }

	/// <summary>Lo que dijo el CCO al rechazar el registro que se corrige.</summary>
	/// <remarks>
	/// Se muestra encima del formulario mientras dura la corrección: sin esto el operador
	/// tendría que volver a la Cola a leer qué tiene que arreglar.
	/// </remarks>
	[ObservableProperty]
	public partial string? MotivoRechazoEnCorreccion { get; set; }

	/// <inheritdoc />
	/// <remarks>
	/// La Cola llega aquí con <c>corregir=&lt;clave&gt;</c>. Solo se anota: la carga la hace
	/// <see cref="InicializarAsync"/>, que corre después y ya con la pantalla lista.
	/// </remarks>
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(ParametroCorregir, out var valor) && valor is string clave
			&& !string.IsNullOrWhiteSpace(clave))
		{
			_claveACorregir = clave;
		}
	}

	/// <summary>Indica si el formulario está corrigiendo un registro rechazado.</summary>
	public bool EstaCorrigiendo => RechazadaEnCorreccion is not null;

	/// <summary>
	/// El botón de borrador no aplica a un rechazado: ya fue incidencia y no vuelve atrás.
	/// </summary>
	public bool MuestraBotonSecundario => !EstaCorrigiendo;

	/// <summary>
	/// Carga en el formulario un registro que el CCO rechazó, para corregirlo y reenviarlo.
	/// </summary>
	/// <remarks>
	/// <b>Es el mismo formulario y casi el mismo camino que abrir un borrador</b>, con dos
	/// diferencias: se muestra el motivo del rechazo mientras se corrige, y el botón principal
	/// reenvía en vez de convertir. Reponer el kilómetro como manual es a propósito, igual que
	/// con el borrador: lo que se repone no es una lectura del GPS.
	/// </remarks>
	private async Task AbrirRechazadaAsync(string clave)
	{
		var rechazada = await _incidencias.ObtenerRechazadaAsync(clave);
		if (rechazada is null)
		{
			// Pudo salir en una tanda entre que se tocó «Corregir» y que se llegó aquí, o ser
			// de otra sesión. Se dice, y se deja el formulario como estaba.
			MensajeError = MensajeRechazadaNoEncontrada;
			return;
		}

		// Un rechazado abierto desplaza a cualquier borrador que estuviera en edición.
		BorradorEnEdicion = null;
		MensajeError = null;
		MensajeEnvio = null;
		RechazadaEnCorreccion = rechazada.ClaveLocal;
		MotivoRechazoEnCorreccion = TextoMotivoRechazo(rechazada);

		// Sus evidencias siguen siendo suyas: están atadas al UUID, que no cambia.
		_uuidParaEvidencias = rechazada.Uuid;
		await RecargarEvidenciasAsync();

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == rechazada.TipoId);
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == rechazada.SeveridadId);
		Nota = rechazada.Nota;
		Kilometro = rechazada.Kilometro ?? string.Empty;
		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}

	/// <summary>
	/// Devuelve el rechazado a la cola con los datos corregidos y vuelve a la Cola.
	/// </summary>
	/// <remarks>
	/// La pantalla no revalida por su cuenta, igual que al convertir: el caso de uso es el
	/// dueño de las comprobaciones y aquí solo se traduce su respuesta. Al terminar se vuelve a
	/// la Cola, que es de donde vino el operador y donde va a ver el registro salir.
	/// </remarks>
	private async Task ReenviarRechazadaAbiertaAsync(string clave)
	{
		var resultado = await _corregirRechazada.EjecutarAsync(
			clave,
			TipoSeleccionado,
			Kilometro,
			FuenteKilometro,
			SeveridadSeleccionada,
			Nota,
			posicionGps: FuenteKilometro == KilometerSource.GPS ? _posicionGps : null);

		if (resultado != ResultadoCorreccionRechazada.Corregida)
		{
			// Si ya no existe, primero se suelta el formulario y después se dice: al revés, la
			// limpieza se llevaba el aviso y el operador veía el formulario vaciarse sin explicación.
			if (resultado == ResultadoCorreccionRechazada.NoEncontrada)
			{
				CancelarCorreccion();
			}

			SenalarError(CampoDe(resultado), MensajeDe(resultado));
			return;
		}

		MensajeError = null;
		CancelarCorreccion();

		// Corregir también deja una incidencia lista, así que también intenta salir en el
		// momento; el aviso del resultado lo verá en la Cola, que es a donde se vuelve.
		await IntentarEnviarRecienGuardadaAsync(clave);
		await Shell.Current.GoToAsync("//principal/cola");
	}

	/// <summary>
	/// Abandona la corrección sin tocar el registro: sigue en Fallido, con su motivo.
	/// </summary>
	[RelayCommand]
	private void CancelarCorreccion()
	{
		RechazadaEnCorreccion = null;
		MotivoRechazoEnCorreccion = null;
		MensajeError = null;
		LimpiarFormulario();
	}

	private static string TextoMotivoRechazo(IncidenciaRechazada rechazada)
	{
		var detalle = !string.IsNullOrWhiteSpace(rechazada.UltimoErrorMensaje)
			? rechazada.UltimoErrorMensaje!.Trim()
			: !string.IsNullOrWhiteSpace(rechazada.UltimoErrorCodigo)
				? $"código {rechazada.UltimoErrorCodigo!.Trim()}"
				: "no se registró el motivo";

		return $"Corrigiendo {rechazada.ClaveLocal}. El CCO la rechazó: {detalle.TrimEnd('.')}. "
			+ "Corrija lo necesario y reenvíela.";
	}

	private string MensajeDe(ResultadoCorreccionRechazada motivo) => motivo switch
	{
		ResultadoCorreccionRechazada.FaltaTipo => MensajeBorradorSinTipo,
		ResultadoCorreccionRechazada.FaltaSeveridad => MensajeBorradorSinSeveridad,
		ResultadoCorreccionRechazada.KilometroInvalido => MensajeKilometroInvalido,
		ResultadoCorreccionRechazada.NotaInsuficiente => MensajeDescripcionRequerida(),
		_ => MensajeRechazadaNoEncontrada,
	};

	/// <summary>Abrir o soltar un borrador cambia lo que los dos botones significan.</summary>
	partial void OnRechazadaEnCorreccionChanged(string? value)
	{
		OnPropertyChanged(nameof(EstaCorrigiendo));
		OnPropertyChanged(nameof(MuestraBotonSecundario));
		OnPropertyChanged(nameof(TextoBotonPrimario));
	}

	private static CampoCaptura? CampoDe(ResultadoCorreccionRechazada resultado) => resultado switch
	{
		ResultadoCorreccionRechazada.FaltaTipo => CampoCaptura.Tipo,
		ResultadoCorreccionRechazada.KilometroInvalido => CampoCaptura.Kilometro,
		ResultadoCorreccionRechazada.FaltaSeveridad => CampoCaptura.Severidad,
		ResultadoCorreccionRechazada.NotaInsuficiente => CampoCaptura.Nota,
		_ => null,
	};
}
