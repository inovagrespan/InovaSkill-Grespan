export const CITY_COORDS: Record<string, { lat: number; lng: number; state: string; region: string }> = {
  // Sudeste
  "São Paulo": { lat: -23.5505, lng: -46.6333, state: "SP", region: "Sudeste" },
  "Sao Paulo": { lat: -23.5505, lng: -46.6333, state: "SP", region: "Sudeste" },
  "Campinas": { lat: -22.9056, lng: -47.0608, state: "SP", region: "Sudeste" },
  "Ribeirão Preto": { lat: -21.1775, lng: -47.8103, state: "SP", region: "Sudeste" },
  "Sorocaba": { lat: -23.5017, lng: -47.4581, state: "SP", region: "Sudeste" },
  "São José dos Campos": { lat: -23.1896, lng: -45.8841, state: "SP", region: "Sudeste" },
  "São José do Rio Preto": { lat: -20.8197, lng: -49.3798, state: "SP", region: "Sudeste" },
  "Bauru": { lat: -22.3218, lng: -49.0706, state: "SP", region: "Sudeste" },
  "Piracicaba": { lat: -22.7253, lng: -47.6489, state: "SP", region: "Sudeste" },
  "Santos": { lat: -23.9608, lng: -46.3336, state: "SP", region: "Sudeste" },
  "Bady Bassitt": { lat: -20.9117, lng: -49.4514, state: "SP", region: "Sudeste" },
  "Rio de Janeiro": { lat: -22.9068, lng: -43.1729, state: "RJ", region: "Sudeste" },
  "Niterói": { lat: -22.8832, lng: -43.1034, state: "RJ", region: "Sudeste" },
  "Belo Horizonte": { lat: -19.9167, lng: -43.9345, state: "MG", region: "Sudeste" },
  "Uberlândia": { lat: -18.9186, lng: -48.2772, state: "MG", region: "Sudeste" },
  "Contagem": { lat: -19.9367, lng: -44.0537, state: "MG", region: "Sudeste" },
  "Juiz de Fora": { lat: -21.7595, lng: -43.3397, state: "MG", region: "Sudeste" },
  "Vitória": { lat: -20.3155, lng: -40.3128, state: "ES", region: "Sudeste" },
  "Vila Velha": { lat: -20.3298, lng: -40.2925, state: "ES", region: "Sudeste" },

  // Sul
  "Curitiba": { lat: -25.4290, lng: -49.2671, state: "PR", region: "Sul" },
  "Londrina": { lat: -23.3103, lng: -51.1628, state: "PR", region: "Sul" },
  "Maringá": { lat: -23.4205, lng: -51.9333, state: "PR", region: "Sul" },
  "Porto Alegre": { lat: -30.0346, lng: -51.2177, state: "RS", region: "Sul" },
  "Caxias do Sul": { lat: -29.1681, lng: -51.1797, state: "RS", region: "Sul" },
  "Florianópolis": { lat: -27.5945, lng: -48.5477, state: "SC", region: "Sul" },
  "Joinville": { lat: -26.3045, lng: -48.8486, state: "SC", region: "Sul" },
  "Blumenau": { lat: -26.9185, lng: -49.0659, state: "SC", region: "Sul" },

  // Nordeste
  "Salvador": { lat: -12.9714, lng: -38.5014, state: "BA", region: "Nordeste" },
  "Feira de Santana": { lat: -12.2669, lng: -38.9668, state: "BA", region: "Nordeste" },
  "Recife": { lat: -8.0476, lng: -34.8770, state: "PE", region: "Nordeste" },
  "Fortaleza": { lat: -3.7172, lng: -38.5433, state: "CE", region: "Nordeste" },
  "São Luís": { lat: -2.5298, lng: -44.3028, state: "MA", region: "Nordeste" },
  "Teresina": { lat: -5.0920, lng: -42.8038, state: "PI", region: "Nordeste" },
  "Natal": { lat: -5.7793, lng: -35.2009, state: "RN", region: "Nordeste" },
  "João Pessoa": { lat: -7.1195, lng: -34.8450, state: "PB", region: "Nordeste" },
  "Maceió": { lat: -9.6498, lng: -35.7089, state: "AL", region: "Nordeste" },
  "Aracaju": { lat: -10.9472, lng: -37.0731, state: "SE", region: "Nordeste" },
  "Campina Grande": { lat: -7.2300, lng: -35.8811, state: "PB", region: "Nordeste" },
  "Petrolina": { lat: -9.3934, lng: -40.5028, state: "PE", region: "Nordeste" },

  // Centro-Oeste
  "Brasília": { lat: -15.7975, lng: -47.8919, state: "DF", region: "Centro-Oeste" },
  "Goiânia": { lat: -16.6864, lng: -49.2643, state: "GO", region: "Centro-Oeste" },
  "Cuiabá": { lat: -15.6010, lng: -56.0974, state: "MT", region: "Centro-Oeste" },
  "Campo Grande": { lat: -20.4435, lng: -54.6475, state: "MS", region: "Centro-Oeste" },

  // Norte
  "Manaus": { lat: -3.1190, lng: -60.0217, state: "AM", region: "Norte" },
  "Belém": { lat: -1.4558, lng: -48.5044, state: "PA", region: "Norte" },
  "Porto Velho": { lat: -8.7619, lng: -63.9039, state: "RO", region: "Norte" },
  "Macapá": { lat: 0.0355, lng: -51.0705, state: "AP", region: "Norte" },
  "Rio Branco": { lat: -9.9740, lng: -67.8077, state: "AC", region: "Norte" },
  "Boa Vista": { lat: 2.8235, lng: -60.6753, state: "RR", region: "Norte" },
  "Palmas": { lat: -10.2491, lng: -48.3243, state: "TO", region: "Norte" },
};

export const REGIONS = ["Todas", "Sudeste", "Sul", "Nordeste", "Centro-Oeste", "Norte"];

export function findCity(search: string): { lat: number; lng: number; state: string; region: string } | null {
  const normalized = search.trim().toLowerCase();
  const match = Object.entries(CITY_COORDS).find(([key]) => key.toLowerCase() === normalized);
  if (match) return match[1];
  const fuzzy = Object.entries(CITY_COORDS).find(([key]) => key.toLowerCase().includes(normalized));
  return fuzzy ? fuzzy[1] : null;
}
