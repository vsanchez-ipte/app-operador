namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Lo que un operador puede hacer dentro de la app (JTT-1385).
/// </summary>
/// <remarks>
/// <para>
/// Son las cuatro que enumera el CA 4. No es la lista de permisos que emite Jacob CCO: es la
/// lista de <b>acciones de la app</b> que hay que autorizar. La correspondencia entre unas y
/// otras la resuelve <see cref="ReglaCapacidades"/>.
/// </para>
/// <para>
/// Se separan porque no son lo mismo y hoy ni siquiera coinciden en número: Jacob emite un
/// único permiso y aquí hay cuatro acciones. Mezclarlos obligaría a repartir por las pantallas
/// el conocimiento de qué código concede qué.
/// </para>
/// </remarks>
public enum CapacidadOperador
{
	/// <summary>Registrar una incidencia de campo.</summary>
	RegistrarIncidencia,

	/// <summary>Adjuntar evidencia a una incidencia.</summary>
	AdjuntarEvidencia,

	/// <summary>Consultar la cola de pendientes.</summary>
	ConsultarCola,

	/// <summary>Iniciar la sincronización con Jacob CCO.</summary>
	Sincronizar,
}
