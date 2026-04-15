namespace AkkornStudio.SqlImport.Ids;

public sealed record IdGenerationMeta(
    string IdSchemeVersion,
    string HashAlgorithm,
    string Encoding,
    int OutputLength
);
