namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Lo único que el operador puede hacer para desbloquear la ubicación desde la pantalla de
/// acceso, según el estado en que se encuentre.
/// </summary>
/// <remarks>
/// Un estado bloqueante sin acción asociada deja al operador atrapado; una acción equivocada
/// —ofrecer "permitir" cuando el sistema ya no va a preguntar— es peor todavía, porque el
/// botón no hace nada visible. Por eso cada estado lleva su acción y no se deduce en la vista.
/// </remarks>
public enum AccionUbicacion
{
	/// <summary>Nada que ofrecer: o ya está concedido, o no depende del operador.</summary>
	Ninguna = 0,

	/// <summary>Pedirle el permiso al sistema, que mostrará su diálogo.</summary>
	SolicitarPermiso = 1,

	/// <summary>Abrir la ficha de configuración de la app, donde está el permiso de ubicación.</summary>
	AbrirAjustesDeLaApp = 2,

	/// <summary>Abrir la configuración de ubicación del dispositivo para encender el servicio.</summary>
	AbrirAjustesDeUbicacion = 3,
}
