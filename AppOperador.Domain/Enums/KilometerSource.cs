namespace AppOperador.Domain.Enums;

/// <summary>
/// Origen del que proviene un punto kilométrico.
/// </summary>
/// <remarks>El nombre está fijado en inglés por el documento de arquitectura.</remarks>
public enum KilometerSource
{
	/// <summary>Obtenido del receptor GPS del dispositivo.</summary>
	GPS = 1,

	/// <summary>Capturado a mano por el operador.</summary>
	Manual = 2,
}
