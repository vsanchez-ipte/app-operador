using System.Collections.ObjectModel;
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
	[ObservableProperty]
	public partial string? BorradorEnEdicion { get; set; }

	public ObservableCollection<RegistroColaVista> Borradores { get; } = [];

	public bool HayBorradores => Borradores.Count > 0;

	[RelayCommand(CanExecute = nameof(PuedeRegistrar))]
	private async Task GuardarBorradorAsync()
	{
		// Un borrador es captura a medias: exige la misma autorización que registrar.
		if (!_capacidades.Puede(CapacidadOperador.RegistrarIncidencia))
		{
			NotificarAutorizacion();
			return;
		}

		// Sin validar: un borrador existe para dejar la captura a medias sin perderla.
		MensajeError = null;

		if (BorradorEnEdicion is { } claveEnEdicion)
		{
			// Editar actualiza el existente; crear otro duplicaría el mismo hecho.
			await _incidencias.ActualizarBorradorAsync(
				claveEnEdicion, TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());
			CancelarEdicionBorrador();
		}
		else
		{
			await _incidencias.GuardarBorradorAsync(
				TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());
			LimpiarFormulario();
		}

		await RecargarBorradoresAsync();
	}

	// Las validaciones son del caso de uso; aquí solo se traduce su respuesta.
	private async Task ConvertirBorradorAbiertoAsync(string clave)
	{
		var resultado = await _convertirBorrador.EjecutarAsync(
			clave,
			TipoSeleccionado,
			Kilometro,
			FuenteKilometro,
			SeveridadSeleccionada,
			Nota,
			posicionGps: FuenteKilometro == KilometerSource.GPS ? _posicionGps : null);

		if (resultado != ResultadoConversionBorrador.Convertido)
		{
			SenalarError(CampoDe(resultado), MensajeDe(resultado));

			// Si ya no existe, el formulario lo suelta.
			if (resultado == ResultadoConversionBorrador.NoEncontrado)
			{
				BorradorEnEdicion = null;
				await RecargarBorradoresAsync();
			}

			return;
		}

		MensajeError = null;
		CancelarEdicionBorrador();
		await RecargarBorradoresAsync();

		// Convertir crea una incidencia, así que también intenta salir en el momento.
		await IntentarEnviarRecienGuardadaAsync(clave);
	}

	public bool EstaEditandoBorrador => BorradorEnEdicion is not null;

	public string TextoBotonSecundario =>
		EstaEditandoBorrador ? "Actualizar borrador" : "Guardar borrador";

	// Mismo formulario: editar un borrador es capturar.
	[RelayCommand]
	private async Task AbrirBorradorAsync(RegistroColaVista? vista)
	{
		if (vista is null)
		{
			return;
		}

		var borrador = await _incidencias.ObtenerBorradorAsync(vista.ClaveLocal);
		if (borrador is null)
		{
			// Pudo eliminarse o ser de otra sesión.
			MensajeError = MensajeBorradorNoEncontrado;
			await RecargarBorradoresAsync();
			return;
		}

		// Borrador y corrección de rechazado no coexisten.
		RechazadaEnCorreccion = null;
		MotivoRechazoEnCorreccion = null;
		MensajeError = null;
		BorradorEnEdicion = borrador.ClaveLocal;

		// Sus evidencias siguen siendo suyas; hay que releerlas porque la lista quedó vacía.
		_uuidParaEvidencias = borrador.Uuid;
		await RecargarEvidenciasAsync();

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == borrador.TipoId);
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == borrador.SeveridadId);
		Nota = borrador.Nota;

		// Se repone como manual: reponerlo no es una lectura del GPS.
		Kilometro = borrador.Kilometro ?? string.Empty;
		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}

	// Sin esta salida, quien abriera un borrador por error quedaría atrapado en él.
	[RelayCommand]
	private void CancelarEdicionBorrador()
	{
		BorradorEnEdicion = null;
		MensajeError = null;
		LimpiarFormulario();
	}

	[RelayCommand]
	private async Task EliminarBorradorAsync()
	{
		if (BorradorEnEdicion is not { } clave)
		{
			return;
		}

		await _eliminarBorrador.EjecutarAsync(clave);
		CancelarEdicionBorrador();
		await RecargarBorradoresAsync();
	}

	private string MensajeDe(ResultadoConversionBorrador motivo) => motivo switch
	{
		ResultadoConversionBorrador.FaltaTipo => MensajeBorradorSinTipo,
		ResultadoConversionBorrador.FaltaSeveridad => MensajeBorradorSinSeveridad,
		ResultadoConversionBorrador.KilometroInvalido => MensajeKilometroInvalido,
		ResultadoConversionBorrador.NotaInsuficiente => MensajeDescripcionRequerida(),
		_ => MensajeBorradorNoEncontrado,
	};

	private async Task RecargarBorradoresAsync()
	{
		Borradores.Clear();
		foreach (var borrador in await _incidencias.ObtenerBorradoresAsync())
		{
			Borradores.Add(new RegistroColaVista(borrador));
		}

		OnPropertyChanged(nameof(HayBorradores));
	}

	partial void OnBorradorEnEdicionChanged(string? value)
	{
		OnPropertyChanged(nameof(EstaEditandoBorrador));
		OnPropertyChanged(nameof(TextoBotonPrimario));
		OnPropertyChanged(nameof(TextoBotonSecundario));
	}

	private static CampoCaptura? CampoDe(ResultadoConversionBorrador resultado) => resultado switch
	{
		ResultadoConversionBorrador.FaltaTipo => CampoCaptura.Tipo,
		ResultadoConversionBorrador.KilometroInvalido => CampoCaptura.Kilometro,
		ResultadoConversionBorrador.FaltaSeveridad => CampoCaptura.Severidad,
		ResultadoConversionBorrador.NotaInsuficiente => CampoCaptura.Nota,
		_ => null,
	};
}
