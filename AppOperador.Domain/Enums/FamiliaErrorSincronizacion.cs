namespace AppOperador.Domain.Enums;

/// <summary>
/// Naturaleza del rechazo de un envío, que es lo que decide si la app reintenta sola
/// (JTT-1401 CA 8 y 9).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>«Funcional» no significa descartar.</b> JTT-1404 CA 4 dice que la ausencia de permiso
/// <b>no elimina el registro pendiente</b>. Ninguna de las dos familias borra nada: lo que las
/// separa es <b>si la app vuelve a intentarlo sola o si espera a que alguien actúe</b>.
/// </para>
/// <para>
/// Vive en el dominio y no junto al cliente HTTP porque la distinción es de negocio, no de
/// transporte. Qué código de Jacob cae en cada familia sí es contrato, y eso se traduce en la
/// capa de aplicación.
/// </para>
/// </remarks>
public enum FamiliaErrorSincronizacion
{
	/// <summary>
	/// El registro es incorrecto y volver a mandarlo igual daría el mismo rechazo.
	/// </summary>
	/// <remarks>
	/// El pendiente <b>se conserva</b> y deja de reintentarse hasta que alguien lo corrija: un
	/// catálogo desactivado, una nota que no alcanza, un kilómetro fuera del corredor. Reintentar
	/// en bucle gastaría batería y datos para obtener catorce veces la misma respuesta.
	/// </remarks>
	Funcional = 1,

	/// <summary>
	/// El registro está bien y el problema es del otro lado o del camino.
	/// </summary>
	/// <remarks>
	/// El pendiente se conserva y <b>se reintenta con espera creciente</b>: red caída, servidor
	/// que no contesta, o una plaza que todavía no resuelve porque a ese ambiente le falta cargar
	/// los rangos. El operador no hizo nada mal y no tiene nada que corregir.
	/// </remarks>
	Tecnico = 2,
}
