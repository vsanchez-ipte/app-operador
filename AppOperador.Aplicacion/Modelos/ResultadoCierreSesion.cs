namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Desenlace de cerrar la sesión móvil (JTT-1390).
/// </summary>
/// <remarks>
/// <b>El cierre local siempre ocurre</b>, así que no hay un caso «no se cerró». Lo único
/// que varía es si a Jacob CCO se le pudo avisar, y eso se informa para la bitácora y para
/// que la pantalla pueda decirlo si conviene.
/// </remarks>
/// <param name="AvisoAlServidor">Si Jacob confirmó la revocación del token.</param>
/// <param name="PendientesConservados">
/// Cuántos registros seguían esperando envío al cerrar. Sirve para dejar constancia de que
/// no se borraron: es el criterio que más importa de esta historia.
/// </param>
public sealed record ResultadoCierreSesion(bool AvisoAlServidor, int PendientesConservados);
