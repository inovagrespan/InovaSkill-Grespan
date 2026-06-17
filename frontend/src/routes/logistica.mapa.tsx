import { useEffect, useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowLeft, MapPin } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { CustomerMap } from "@/components/ui/customer-map";
import { authFetch } from "@/lib/auth";
import { buildServiceUrl } from "@/lib/api-url";

export const Route = createFileRoute("/logistica/mapa")({ component: MapaPage });

function MapaPage() {
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    setLoaded(true);
  }, []);

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Logística / Mapa</span>
          <h1 className="text-3xl font-display font-semibold tracking-tight mt-1">Mapa de clientes</h1>
          <p className="text-sm text-muted-foreground mt-1">Distribuição geográfica por cidade e região.</p>
        </div>
        <Button variant="outline" asChild>
          <Link to="/logistica"><ArrowLeft className="w-4 h-4 mr-2" />Voltar para rotas</Link>
        </Button>
      </header>

      <section className="animate-soft-enter">
        <Card>
          <CardContent className="p-4 sm:p-6">
            {!loaded ? (
              <Skeleton className="h-[420px] rounded-xl" />
            ) : (
              <CustomerMap customers={DEMO_MAPA} />
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

const DEMO_MAPA = [
  // Sudeste - SP
  { name: "Padaria São Bento", city: "Campinas", revenue: 64850, orders: 28 },
  { name: "Mercado Central", city: "Campinas", revenue: 28400, orders: 15 },
  { name: "Supermercado Primavera", city: "Ribeirão Preto", revenue: 52300, orders: 21 },
  { name: "Açougue do João", city: "Ribeirão Preto", revenue: 19800, orders: 9 },
  { name: "Cafeteria Grão & Massa", city: "Sorocaba", revenue: 38940, orders: 18 },
  { name: "Restaurante Sabor", city: "Sorocaba", revenue: 17500, orders: 8 },
  { name: "Rede Conveniência Rota 12", city: "São Paulo", revenue: 31500, orders: 12 },
  { name: "Padaria da Vila", city: "São Paulo", revenue: 22100, orders: 11 },
  { name: "Distribuidora Vale", city: "São José dos Campos", revenue: 33400, orders: 14 },
  { name: "Padaria Rio Preto", city: "São José do Rio Preto", revenue: 25600, orders: 11 },
  { name: "Mercado Bauru", city: "Bauru", revenue: 19800, orders: 9 },
  { name: "Comercial Piracicaba", city: "Piracicaba", revenue: 27400, orders: 12 },
  { name: "Peixaria Porto", city: "Santos", revenue: 22100, orders: 10 },
  { name: "Mercado Bady", city: "Bady Bassitt", revenue: 16200, orders: 7 },
  { name: "Empório Bady", city: "Bady Bassitt", revenue: 12100, orders: 5 },
  // Sudeste - RJ
  { name: "Distribuidora Mar Vermelho", city: "Rio de Janeiro", revenue: 87200, orders: 34 },
  { name: "Mercado Niterói", city: "Niterói", revenue: 31200, orders: 14 },
  // Sudeste - MG
  { name: "Empório Minas", city: "Belo Horizonte", revenue: 69500, orders: 27 },
  { name: "Supermercado Triângulo", city: "Uberlândia", revenue: 31200, orders: 12 },
  { name: "Distribuidora Contagem", city: "Contagem", revenue: 24500, orders: 11 },
  { name: "Comercial Juiz de Fora", city: "Juiz de Fora", revenue: 21800, orders: 10 },
  // Sudeste - ES
  { name: "Peixaria Vitória", city: "Vitória", revenue: 28900, orders: 13 },
  { name: "Mercado Vila Velha", city: "Vila Velha", revenue: 19600, orders: 9 },
  // Sul - PR
  { name: "Friozão Distribuidora", city: "Curitiba", revenue: 52300, orders: 23 },
  { name: "Supermercado Londrina", city: "Londrina", revenue: 31500, orders: 13 },
  { name: "Comercial Maringá", city: "Maringá", revenue: 27800, orders: 12 },
  // Sul - RS
  { name: "Supermercado Sul", city: "Porto Alegre", revenue: 38700, orders: 17 },
  { name: "Distribuidora Caxias", city: "Caxias do Sul", revenue: 23400, orders: 11 },
  // Sul - SC
  { name: "Padaria Ilha", city: "Florianópolis", revenue: 28900, orders: 13 },
  { name: "Mercado Joinville", city: "Joinville", revenue: 34100, orders: 15 },
  { name: "Comercial Blumenau", city: "Blumenau", revenue: 22300, orders: 10 },
  // Nordeste - BA
  { name: "Mercado do Porto", city: "Salvador", revenue: 45100, orders: 19 },
  { name: "Comercial Feira", city: "Feira de Santana", revenue: 21500, orders: 10 },
  // Nordeste - PE
  { name: "Mercado Nordestino", city: "Recife", revenue: 51200, orders: 22 },
  { name: "Comercial Petrolina", city: "Petrolina", revenue: 18700, orders: 8 },
  // Nordeste - CE
  { name: "Comercial Fortaleza", city: "Fortaleza", revenue: 47600, orders: 20 },
  // Nordeste - MA
  { name: "Mercado São Luís", city: "São Luís", revenue: 29800, orders: 13 },
  // Nordeste - PI
  { name: "Comercial Teresina", city: "Teresina", revenue: 23100, orders: 11 },
  // Nordeste - RN
  { name: "Mercado Natal", city: "Natal", revenue: 26700, orders: 12 },
  // Nordeste - PB
  { name: "Comercial João Pessoa", city: "João Pessoa", revenue: 25400, orders: 11 },
  { name: "Mercado Campina Grande", city: "Campina Grande", revenue: 18300, orders: 8 },
  // Nordeste - AL
  { name: "Mercado Maceió", city: "Maceió", revenue: 24100, orders: 11 },
  // Nordeste - SE
  { name: "Comercial Aracaju", city: "Aracaju", revenue: 20800, orders: 9 },
  // Centro-Oeste - DF
  { name: "Comercial Planalto", city: "Brasília", revenue: 78400, orders: 31 },
  // Centro-Oeste - GO
  { name: "Mercado do Cerrado", city: "Goiânia", revenue: 46800, orders: 20 },
  // Centro-Oeste - MT
  { name: "Comercial Cuiabá", city: "Cuiabá", revenue: 29500, orders: 13 },
  // Centro-Oeste - MS
  { name: "Mercado Campo Grande", city: "Campo Grande", revenue: 27600, orders: 12 },
  // Norte - AM
  { name: "Distribuidora Norte", city: "Manaus", revenue: 42300, orders: 16 },
  // Norte - PA
  { name: "Comercial Belém", city: "Belém", revenue: 35600, orders: 14 },
  // Norte - RO
  { name: "Mercado Porto Velho", city: "Porto Velho", revenue: 18700, orders: 8 },
  // Norte - AP
  { name: "Comercial Macapá", city: "Macapá", revenue: 14300, orders: 6 },
  // Norte - AC
  { name: "Mercado Rio Branco", city: "Rio Branco", revenue: 12100, orders: 5 },
  // Norte - RR
  { name: "Comercial Boa Vista", city: "Boa Vista", revenue: 13500, orders: 6 },
  // Norte - TO
  { name: "Mercado Palmas", city: "Palmas", revenue: 16200, orders: 7 },
];
