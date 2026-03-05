from http.server import HTTPServer, BaseHTTPRequestHandler
from datetime import datetime
import xml.etree.ElementTree as ET
import hashlib


class GdcHandler(BaseHTTPRequestHandler):

    def log_message(self, format, *args):
        timestamp = datetime.now().strftime("%H:%M:%S")
        print(f"[GDC {timestamp}] {format % args}")

    def do_POST(self):
        try:
            content_length = int(self.headers.get('Content-Length', 0))
            if content_length == 0:
                self.send_error(400, "Missing Content-Length")
                return

            body = self.rfile.read(content_length).decode('utf-8')

            try:
                root = ET.fromstring(body)
                ns = {'soap': 'http://schemas.xmlsoap.org/soap/envelope/'}
                body_elem = root.find('.//soap:Body', ns)

                response_body = None

                if body_elem is not None and len(body_elem):
                    req_elem = list(body_elem)[0]
                    tag = req_elem.tag.split('}')[-1]

                    if tag == 'searchEntities':
                        id_activo = req_elem.findtext('.//{*}value') or ''
                        md5 = ''

                        # Extract md5 from a second value element when present
                        all_values = req_elem.findall('.//{*}value')
                        if all_values and len(all_values) > 1:
                            md5 = (all_values[1].text or '').strip()

                        print(f"GDC searchEntities: id_activo={id_activo} md5={md5}")

                        if 'exists' in id_activo.lower() or 'already' in md5.lower():
                            object_id = f"GDC-{hashlib.sha1((id_activo + '|' + md5).encode()).hexdigest()[:12]}"
                            response_body = (
                                "<searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\">"
                                "<return>"
                                f"<entities><entityId>{object_id}</entityId></entities>"
                                "</return>"
                                "</searchEntitiesResponse>"
                            )
                        else:
                            response_body = (
                                "<searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\">"
                                "<return></return>"
                                "</searchEntitiesResponse>"
                            )

                    elif tag == 'ConsultarDocumentoRequest':
                        id_activo = req_elem.findtext('.//IdActivo') or ''
                        matricula = req_elem.findtext('.//Matricula') or ''
                        print(f"GDC Consultar: IdActivo={id_activo} Matricula={matricula}")

                        if 'exists' in id_activo.lower():
                            object_id = f"GDC-{hashlib.sha1(id_activo.encode()).hexdigest()[:12]}"
                            response_body = f"<ConsultarDocumentoResponse xmlns=\"http://sintws.example.org/\"><ObjectId>{object_id}</ObjectId></ConsultarDocumentoResponse>"
                        else:
                            response_body = f"<ConsultarDocumentoResponse xmlns=\"http://sintws.example.org/\"></ConsultarDocumentoResponse>"

                    elif tag == 'SubirDocumentoRequest':
                        id_activo = req_elem.findtext('.//IdActivo') or ''
                        nombre = req_elem.findtext('.//NombreArchivo') or 'file'
                        print(f"GDC Subir: IdActivo={id_activo} Nombre={nombre}")

                        if 'already' in nombre.lower() or 'already' in id_activo.lower():
                            response_body = f"<SubirDocumentoResponse xmlns=\"http://sintws.example.org/\"><AlreadyExists>1</AlreadyExists></SubirDocumentoResponse>"
                        else:
                            seed = f"{id_activo}|{nombre}|{datetime.utcnow().isoformat()}"
                            object_id = f"GDC-{hashlib.sha1(seed.encode()).hexdigest()[:12]}"
                            response_body = f"<SubirDocumentoResponse xmlns=\"http://sintws.example.org/\"><ObjectId>{object_id}</ObjectId></SubirDocumentoResponse>"

                if response_body is None:
                    response_body = f"<Response xmlns=\"http://tempuri.org/\"><Fecha>{datetime.now().isoformat()}</Fecha></Response>"

                soap_response = '<?xml version="1.0" encoding="utf-8"?>' + \
                    '<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">' + \
                    '<soap:Body>' + response_body + '</soap:Body></soap:Envelope>'

                self.send_response(200)
                self.send_header('Content-Type', 'text/xml; charset=utf-8')
                self.end_headers()
                self.wfile.write(soap_response.encode('utf-8'))

            except Exception as ex:
                print(f"Error processing GDC request: {ex}")
                self.send_error(500, str(ex))

        except Exception as e:
            print(f"GDC server error: {e}")
            self.send_error(500, str(e))


def run_server(port=8083):
    server_address = ('', port)
    httpd = HTTPServer(server_address, GdcHandler)
    print('='*60)
    print('MOCK GDC SERVER')
    print('='*60)
    print(f'Listening on http://localhost:{port}')
    print('Supports: searchEntities, ConsultarDocumentoRequest, SubirDocumentoRequest')
    print('='*60)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print('\nStopping GDC mock...')
        httpd.shutdown()


if __name__ == '__main__':
    run_server(8083)
