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

public sealed partial class CapturaViewModel
{
	public const string ParametroCorregir = "corregir";

	// Se guarda hasta terminar de inicializar: si no, la ubicación recalculada pisaría el KM repuesto.
	private string? _claveACorregir;

	// Modo distinto de editar borrador: no se guarda como borrador ni se elimina, se reenvía.
	[ObservableProperty]
	public partial string? RechazadaEnCorreccion { get; set; }

	// Visible mientras corrige, para no volver a la Cola a leer qué arreglar.
	[ObservableProperty]
	public partial string? MotivoRechazoEnCorreccion { get; set; }

	// Solo anota la clave; la carga la hace InicializarAsync con la pantalla lista.
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(ParametroCorregir, out var valor) && valor is string clave
			&& !string.IsNullOrWhiteSpace(clave))
		{
			_claveACorregir = clave;
		}
	}

	public bool EstaCorrigiendo => RechazadaEnCorreccion is not null;

	public bool MuestraBotonSecundario => !EstaCorrigiendo;

	private async Task AbrirRechazadaAsync(string clave)
	{
		var rechazada = await _incidencias.ObtenerRechazadaAsync(clave);
		if (rechazada is null)
		{
			// Pudo salir en una tanda o ser de otra sesión.
			MensajeError = MensajeRechazadaNoEncontrada;
			return;
		}

		// Un rechazado abierto desplaza al borrador en edición.
		BorradorEnEdicion = null;
		MensajeError = null;
		MensajeEnvio = null;
		RechazadaEnCorreccion = rechazada.ClaveLocal;
		MotivoRechazoEnCorreccion = TextoMotivoRechazo(rechazada);

		// Sus evidencias siguen atadas al mismo UUID.
		_uuidParaEvidencias = rechazada.Uuid;
		await RecargarEvidenciasAsync();

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == rechazada.TipoId);
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == rechazada.SeveridadId);
		Nota = rechazada.Nota;
		Kilometro = rechazada.Kilometro ?? string.Empty;
		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}

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
			// Primero se suelta el formulario y después se avisa; al revés, la limpieza borraba el aviso.
			if (resultado == ResultadoCorreccionRechazada.NoEncontrada)
			{
				CancelarCorreccion();
			}

			SenalarError(CampoDe(resultado), MensajeDe(resultado));
			return;
		}

		MensajeError = null;
		CancelarCorreccion();

		// También intenta salir en el momento; el resultado se ve en la Cola.
		await IntentarEnviarRecienGuardadaAsync(clave);
		await Shell.Current.GoToAsync("//principal/cola");
	}

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
