import { ALL_TOLL_PLAZAS, automaticTollTariff, type TollPlaza } from "./logistics-map-data";

const EARTH_RADIUS_METERS = 6_371_000;
const TOLL_ROUTE_MATCH_RADIUS_METERS = 1_500;
const DEFAULT_COMMERCIAL_AXLES = 2;

export type RouteTollCost = {
  plaza: TollPlaza;
  passages: number;
  automaticCost: number;
};

export type RouteTollEstimate = {
  tolls: RouteTollCost[];
  totalAutomaticCost: number;
  totalPassages: number;
};

type Coordinate = [longitude: number, latitude: number];

function distanceToSegmentMeters(point: Coordinate, start: Coordinate, end: Coordinate): number {
  const latitudeRadians = (point[1] * Math.PI) / 180;
  const scaleX = Math.cos(latitudeRadians) * EARTH_RADIUS_METERS * (Math.PI / 180);
  const scaleY = EARTH_RADIUS_METERS * (Math.PI / 180);
  const px = point[0] * scaleX;
  const py = point[1] * scaleY;
  const sx = start[0] * scaleX;
  const sy = start[1] * scaleY;
  const ex = end[0] * scaleX;
  const ey = end[1] * scaleY;
  const dx = ex - sx;
  const dy = ey - sy;
  const lengthSquared = dx * dx + dy * dy;
  const projection = lengthSquared === 0 ? 0 : Math.max(0, Math.min(1, ((px - sx) * dx + (py - sy) * dy) / lengthSquared));
  const closestX = sx + projection * dx;
  const closestY = sy + projection * dy;
  return Math.hypot(px - closestX, py - closestY);
}

function isNearPlaza(coordinates: Coordinate[], plaza: TollPlaza): boolean {
  const point: Coordinate = [plaza.lng, plaza.lat];
  return coordinates.some((coordinate, index) => index === 0
    ? distanceToSegmentMeters(point, coordinate, coordinate) <= TOLL_ROUTE_MATCH_RADIUS_METERS
    : distanceToSegmentMeters(point, coordinates[index - 1], coordinate) <= TOLL_ROUTE_MATCH_RADIUS_METERS);
}

function countPassages(coordinates: Coordinate[], plaza: TollPlaza): number {
  let passages = 0;
  let wasNear = false;
  const point: Coordinate = [plaza.lng, plaza.lat];
  for (let index = 0; index < coordinates.length; index += 1) {
    const near = index === 0
      ? distanceToSegmentMeters(point, coordinates[index], coordinates[index]) <= TOLL_ROUTE_MATCH_RADIUS_METERS
      : distanceToSegmentMeters(point, coordinates[index - 1], coordinates[index]) <= TOLL_ROUTE_MATCH_RADIUS_METERS;
    if (near && !wasNear) passages += 1;
    wasNear = near;
  }
  return passages;
}

export function estimateRouteTollCost(
  coordinates: Coordinate[],
  commercialAxles = DEFAULT_COMMERCIAL_AXLES,
): RouteTollEstimate {
  if (coordinates.length === 0) return { tolls: [], totalAutomaticCost: 0, totalPassages: 0 };
  const tolls = ALL_TOLL_PLAZAS.flatMap((plaza) => {
    if (!isNearPlaza(coordinates, plaza)) return [];
    const passages = countPassages(coordinates, plaza);
    const manualCost = plaza.commercialManualByAxle[commercialAxles] ?? plaza.commercialManualByAxle[DEFAULT_COMMERCIAL_AXLES];
    return [{ plaza, passages, automaticCost: automaticTollTariff(plaza, manualCost) }];
  });
  return {
    tolls,
    totalAutomaticCost: tolls.reduce((total, toll) => total + toll.automaticCost * toll.passages, 0),
    totalPassages: tolls.reduce((total, toll) => total + toll.passages, 0),
  };
}

export const routeTollPolicy = {
  matchRadiusMeters: TOLL_ROUTE_MATCH_RADIUS_METERS,
  defaultCommercialAxles: DEFAULT_COMMERCIAL_AXLES,
} as const;
