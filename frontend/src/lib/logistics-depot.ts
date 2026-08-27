export type LogisticsDepotForm = {
  name: string;
  address: string;
  latitude: string;
  longitude: string;
};

export type ValidLogisticsDepot = {
  name: string;
  address: string;
  latitude: number;
  longitude: number;
};

function coordinate(value: string): number {
  return Number(value.trim().replace(",", "."));
}

export function validateLogisticsDepotForm(
  form: LogisticsDepotForm,
): { value: ValidLogisticsDepot | null; error: string | null } {
  const name = form.name.trim();
  const address = form.address.trim();
  if (!name) return { value: null, error: "Informe o nome do depósito." };
  if (!address) return { value: null, error: "Informe o endereço do depósito." };
  const latitude = coordinate(form.latitude);
  const longitude = coordinate(form.longitude);
  if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90)
    return { value: null, error: "Informe uma latitude válida entre -90 e 90." };
  if (!Number.isFinite(longitude) || longitude < -180 || longitude > 180)
    return { value: null, error: "Informe uma longitude válida entre -180 e 180." };
  return { value: { name, address, latitude, longitude }, error: null };
}
