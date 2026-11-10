using System;

// DTOs de POST /api/partidas/. Contrato: Docs/contratos/resultado-partida.schema.json

[Serializable]
public class ParticipanteDto
{
    public int id;
    public int puntos;
    public int kills;
    public string resultado; // "victoria", "derrota" o "empate"
    public bool abandono;
}

[Serializable]
public class ReportePartidaDto
{
    public string match_id;
    public bool por_abandono;
    public ParticipanteDto[] jugadores;
}

[Serializable]
public class PartidaCreadaDto
{
    public int partida;
}
