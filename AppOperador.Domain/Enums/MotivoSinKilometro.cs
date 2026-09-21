namespace AppOperador.Domain.Enums;

/// <summary>
/// Por qué no se pudo poner el kilómetro a partir de la ubicación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe para que la pantalla pueda decir algo útil.</b> Los cinco casos del CA 1 de
/// JTT-1396 son cinco cosas distintas para el operador: una la arregla él en dos toques, otra no
/// tiene arreglo y otra no es culpa suya. Un aviso único —«No se pudo obtener el GPS»— lo manda a
/// mirar al cielo cuando lo que pasa es que no dio el permiso, y **enseña a ignorar el aviso**.
/// </para>
/// <para>
/// Es el mismo patrón que <c>ResultadoSondeo</c> en la conectividad y <c>ResultadoEnvio</c> en el
/// envío: el motivo viaja con el fallo en vez de perderse en un nulo.
/// </para>
/// </remarks>
public enum MotivoSinKilometro
{
	/// <summary>El dispositivo no tiene servicio de ubicación, o está apagado.</summary>
	/// <remarks>JTT-1396 CA 1, «la ubicación no está disponible».</remarks>
	ServicioNoDisponible = 1,

	/// <summary>El operador no concedió el permiso de ubicación, o lo revocó.</summary>
	/// <remarks>
	/// JTT-1396 CA 1, «el permiso no permite obtener una posición». <b>Tiene arreglo y el
	/// operador puede hacerlo</b>: es el único motivo que justifica mandarlo a los ajustes.
	/// </remarks>
	PermisoDenegado = 2,

	/// <summary>Hubo lectura, pero su precisión no alcanza la tolerancia.</summary>
	/// <remarks>
	/// JTT-1395 CA 12 y JTT-1396 CA 1. Suele arreglarse solo en unos segundos, así que el aviso
	/// invita a reintentar en vez de dar el GPS por perdido.
	/// </remarks>
	PrecisionInsuficiente = 3,

	/// <summary>La lectura es buena, pero el vehículo no está sobre el corredor.</summary>
	/// <remarks>
	/// JTT-1395 CA 6. <b>No hay nada que arreglar</b>: pasa en el tramo de acceso a la caseta y
	/// en cualquier camino vecino. El aviso no debe alarmar.
	/// </remarks>
	FueraDelCorredor = 4,

	/// <summary>
	/// La lectura es buena y el vehículo puede estar en el corredor, pero de ese tramo no hay
	/// geometría cargada.
	/// </summary>
	/// <remarks>
	/// <b>Se separa de <see cref="FueraDelCorredor"/> a propósito.</b> El KMZ entregado cubre
	/// Tijuana–Tecate, y el corredor del que habla el CA 3 es Tijuana–Mexicali: decirle a un
	/// operador de Mexicali que está «fuera del corredor» sería culparlo de una carencia del
	/// sistema, y acabaría reportándolo como defecto.
	/// </remarks>
	TramoSinGeometria = 5,

	/// <summary>Se pidió la posición y el intento falló.</summary>
	/// <remarks>JTT-1396 CA 1, «el cálculo de KM falla». Es la red de seguridad.</remarks>
	ErrorAlObtener = 6,
}
