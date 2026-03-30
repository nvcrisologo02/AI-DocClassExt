from http.server import HTTPServer, BaseHTTPRequestHandler
from datetime import datetime
import xml.etree.ElementTree as ET

class SoapHandler(BaseHTTPRequestHandler):
    
    def log_message(self, format, *args):
        timestamp = datetime.now().strftime("%H:%M:%S")
        print(f"[{timestamp}] {format % args}")
    
    def do_POST(self):
        try:
            content_length = int(self.headers.get('Content-Length', 0))
            if content_length == 0:
                self.send_error(400, "Missing Content-Length")
                return
            
            # Leer SOAP request
            body = self.rfile.read(content_length).decode('utf-8')
            
            print("\n" + "="*60)
            print("SOAP REQUEST RECIBIDO")
            print("="*60)
            
            # Parsear XML
            try:
                root = ET.fromstring(body)
                
                # Extraer datos del Body
                ns = {'soap': 'http://schemas.xmlsoap.org/soap/envelope/'}
                body_elem = root.find('.//soap:Body', ns)
                
                if body_elem is not None:
                    print("Campos recibidos:")
                    for child in body_elem.iter():
                        if child.text and child.text.strip():
                            print(f"  - {child.tag}: {child.text}")
                
            except Exception as e:
                print(f"Error parseando XML: {e}")
            
            # Construir respuesta SOAP
            soap_response = '''<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
    <soap:Body>
        <Response xmlns="http://tempuri.org/">
            <SuperficieCatastral>87.5</SuperficieCatastral>
            <ValorCatastral>185000</ValorCatastral>
            <CoordenadasGPS>40.4168,-3.7038</CoordenadasGPS>
            <ProvinciaRegistro>Madrid</ProvinciaRegistro>
            <FechaConsulta>{}</FechaConsulta>
            <EstadoFinca>ACTIVA</EstadoFinca>
            <UsoSuelo>RESIDENCIAL</UsoSuelo>
        </Response>
    </soap:Body>
</soap:Envelope>'''.format(datetime.now().strftime("%Y-%m-%dT%H:%M:%S"))
            
            # Enviar respuesta
            self.send_response(200)
            self.send_header('Content-Type', 'text/xml; charset=utf-8')
            self.end_headers()
            self.wfile.write(soap_response.encode('utf-8'))
            
            print("\nSOAP RESPONSE enviado: 7 campos")
            print("="*60 + "\n")
            
        except Exception as e:
            print(f"\nERROR: {str(e)}\n")
            self.send_error(500, str(e))

def run_server(port=8081):
    server_address = ('', port)
    httpd = HTTPServer(server_address, SoapHandler)
    
    print("="*60)
    print("MOCK SOAP SERVER")
    print("="*60)
    print(f"Escuchando en: http://localhost:{port}")
    print(f"Tipo: SOAP 1.1")
    print(f"Namespace: http://tempuri.org/")
    print(f"\nPresiona Ctrl+C para detener")
    print("="*60 + "\n")
    
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n\nDeteniendo servidor...")
        httpd.shutdown()

if __name__ == '__main__':
    run_server(8081)
