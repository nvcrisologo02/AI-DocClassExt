from http.server import HTTPServer, BaseHTTPRequestHandler
import json
from datetime import datetime

class EnrichmentHandler(BaseHTTPRequestHandler):
    
    def log_message(self, format, *args):
        """Override para logging mas limpio"""
        timestamp = datetime.now().strftime("%H:%M:%S")
        print(f"[{timestamp}] {format % args}")
    
    def do_POST(self):
        try:
            # Leer Content-Length de forma segura
            content_length = self.headers.get('Content-Length')
            
            if content_length is None:
                self.send_error(400, "Missing Content-Length header")
                return
            
            content_length = int(content_length)
            
            # Leer body
            post_data = self.rfile.read(content_length)
            
            if not post_data:
                self.send_error(400, "Empty request body")
                return
            
            # Parsear JSON
            try:
                request_data = json.loads(post_data.decode('utf-8'))
            except json.JSONDecodeError as e:
                self.send_error(400, f"Invalid JSON: {str(e)}")
                return
            
            # Obtener datos extraidos
            datos_originales = request_data.get('datosExtraidos', {})
            tipologia = request_data.get('tipologia', 'unknown')
            documento_id = request_data.get('documentoId', 'unknown')
            
            print(f"\n{'='*60}")
            print(f"SOLICITUD DE ENRIQUECIMIENTO")
            print(f"{'='*60}")
            print(f"Documento ID: {documento_id}")
            print(f"Tipologia:    {tipologia}")
            print(f"Campos recibidos: {len(datos_originales)}")
            print(f"Campos: {', '.join(datos_originales.keys())}")
            
            # Crear copia para enriquecer
            datos_enriquecidos = dict(datos_originales)
            
            # ENRIQUECER: Agregar campos nuevos segun tipologia
            if 'notasimple' in tipologia.lower():
                datos_enriquecidos['IdActivo'] = f'ACT-NS-{datetime.now().strftime("%Y%m%d")}-001'
                datos_enriquecidos['IdProyecto'] = 'PRJ-REGISTRO-MADRID'
                datos_enriquecidos['EstadoActivo'] = 'PENDIENTE_REVISION'
                datos_enriquecidos['TipoGestion'] = 'NOTA_SIMPLE'
                
                # Si hay referencia catastral, agregar info
                if 'ReferenciaCatastral' in datos_originales:
                    datos_enriquecidos['ValidacionCatastral'] = 'PENDIENTE'
                    datos_enriquecidos['OrigenReferencia'] = 'CATASTRO'
                
            elif 'tasacion' in tipologia.lower():
                datos_enriquecidos['IdActivo'] = f'ACT-TAS-{datetime.now().strftime("%Y%m%d")}-001'
                datos_enriquecidos['IdProyecto'] = 'PRJ-VALORACION-2024'
                datos_enriquecidos['EstadoActivo'] = 'EN_TASACION'
                datos_enriquecidos['TipoGestion'] = 'TASACION'
                
                # Si hay valor tasado, calcular rango
                if 'ValorTasado' in datos_originales:
                    valor = float(datos_originales['ValorTasado'])
                    if valor < 100000:
                        datos_enriquecidos['RangoValor'] = 'BAJO'
                    elif valor < 500000:
                        datos_enriquecidos['RangoValor'] = 'MEDIO'
                    else:
                        datos_enriquecidos['RangoValor'] = 'ALTO'
            
            else:
                # Enriquecimiento generico
                datos_enriquecidos['IdActivo'] = f'ACT-GEN-{datetime.now().strftime("%Y%m%d")}-001'
                datos_enriquecidos['IdProyecto'] = 'PRJ-GENERAL'
                datos_enriquecidos['EstadoActivo'] = 'NUEVO'
            
            # Agregar campos comunes de enriquecimiento
            datos_enriquecidos['FechaEnriquecimiento'] = datetime.now().isoformat()
            datos_enriquecidos['EnriquecidoPor'] = 'MockServer-Atlas'
            datos_enriquecidos['VersionEnriquecimiento'] = '1.0'
            
            # Si hay NIF, validar formato basico
            if 'NIF' in datos_originales and datos_originales['NIF']:
                nif = str(datos_originales['NIF'])
                if len(nif) == 9:
                    datos_enriquecidos['TitularValidado'] = True
                    datos_enriquecidos['TipoPersona'] = 'FISICA' if nif[0].isdigit() else 'JURIDICA'
                else:
                    datos_enriquecidos['TitularValidado'] = False
                    datos_enriquecidos['TipoPersona'] = 'DESCONOCIDO'
            
            # Calcular campos agregados
            campos_agregados = len(datos_enriquecidos) - len(datos_originales)
            
            print(f"\nENRIQUECIMIENTO APLICADO:")
            print(f"  Campos agregados: {campos_agregados}")
            print(f"  Total campos: {len(datos_enriquecidos)}")
            
            # Mostrar campos nuevos
            nuevos_campos = set(datos_enriquecidos.keys()) - set(datos_originales.keys())
            if nuevos_campos:
                print(f"  Nuevos campos: {', '.join(nuevos_campos)}")
            
            # Responder con datos enriquecidos
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('X-Enrichment-Server', 'MockServer')
            self.send_header('X-Fields-Added', str(campos_agregados))
            self.end_headers()
            
            response_json = json.dumps(datos_enriquecidos, indent=2, ensure_ascii=False)
            self.wfile.write(response_json.encode('utf-8'))
            
            print(f"{'='*60}\n")
            
        except Exception as e:
            print(f"\nERROR procesando request: {str(e)}")
            import traceback
            traceback.print_exc()
            
            self.send_response(500)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            
            error_response = {
                'error': 'Internal Server Error',
                'message': str(e),
                'type': type(e).__name__
            }
            self.wfile.write(json.dumps(error_response).encode('utf-8'))
    
    def do_GET(self):
        """Health check endpoint"""
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        
        health = {
            'status': 'OK',
            'service': 'Mock Enrichment Server',
            'version': '1.0',
            'timestamp': datetime.now().isoformat()
        }
        self.wfile.write(json.dumps(health, indent=2).encode('utf-8'))

def run_server(port=8080):
    server_address = ('', port)
    httpd = HTTPServer(server_address, EnrichmentHandler)
    
    print("="*60)
    print("MOCK ENRICHMENT SERVER")
    print("="*60)
    print(f"Servidor escuchando en: http://localhost:{port}")
    print(f"Health check: http://localhost:{port}/health")
    print(f"\nSoporta enriquecimiento para:")
    print("  - Nota Simple (notasimple)")
    print("  - Tasacion (tasacion)")
    print("  - Documentos genericos")
    print(f"\nPresiona Ctrl+C para detener")
    print("="*60 + "\n")
    
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n\nDeteniendo servidor...")
        httpd.shutdown()
        print("Servidor detenido.")

if __name__ == '__main__':
    run_server(8080)
