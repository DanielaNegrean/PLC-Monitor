import asyncio
import sys
import os
from pathlib import Path
import PySide6
from PySide6.QtCore import QFile, QIODevice
from PySide6.QtUiTools import QUiLoader
from PySide6.QtWidgets import QApplication, QFileDialog, QMainWindow, QMessageBox, QWidget
from qasync import QEventLoop, asyncSlot
from asyncua import Client

class FereastraPrincipala(QMainWindow):
    def __init__(self):
        super().__init__()

        script_dir = Path(__file__).resolve().parent
        ui_path = script_dir/"interfata.ui"
        
        # 1. Încărcăm design-ul făcut în Qt Designer
        ui_file = QFile(str(ui_path))
        if not ui_file.open(QIODevice.ReadOnly):
            print(f"ERROR: Could not open the UI file at the path: \n{ui_path}")
            print("Check if the file exists in the 'src' folder")
            return
        
        # Load the interface with QuiLoader
        loader = QUiLoader()
        self.ui = loader.load(ui_file)
        ui_file.close()
        if self.ui is None:
            raise RuntimeError(f"Could not load the UI file: {ui_path}")

        self.setCentralWidget(self.ui)
        self.setWindowTitle(self.ui.windowTitle())
        self.resize(self.ui.size())
        for widget_name in (
            "btn_conectare",
            "btn_stop",
            "label_valoare",

            "pln_text_PLC_addres",
            "pln_text_PLC_port",

            "txt_recirculare",
            "txt_centrala_gaz",
            "txt_pompa_puffer",
            "txt_pompa_caldura",
            "txt_recirculare_lastday",
            "txt_centrala_gaz_lastday",
            "txt_pompa_puffer_lastday",
            "txt_pompa_caldura_lastday",
            "txt_incalzire_sub",
            "txt_incalzire_parter",
            "txt_incalzire_etaj",
            "txt_incalzire_pod",
            "txt_incalzire_sub_lastday",
            "txt_incalzire_parter_lastday",
            "txt_incalzire_etaj_lastday",
            "txt_incalzire_pod_lastday",

            "lb_recirculare",
            "lb_centrala_gaz",
            "lb_pompa_puffer",
            "lb_pompa_caldura",
            "lb_recirculare_lastday",
            "lb_centrala_gaz_lastday",
            "lb_pompa_puffer_lastday",
            "lb_pompa_caldura_lastday",
            "lb_incalzire_sub",
            "lb_incalzire_parter",
            "lb_incalzire_etaj",
            "lb_incalzire_pod",
            "lb_incalzire_sub_lastday",
            "lb_incalzire_parter_lastday",
            "lb_incalzire_etaj_lastday",
            "lb_incalzire_pod_lastday",
            "lb_ip_plc_port",
            "lb_ip_plc",
        ):
            widget = self.ui.findChild(QWidget, widget_name)
            if widget is None:
                raise RuntimeError(
                    f"Widget '{widget_name}' is missing from the UI file: {ui_path}"
                )
            setattr(self.ui, widget_name, widget)

        # Make all buttons green
        self.ui.setStyleSheet(
            """
            QPushButton {
                background-color: #0078D4;
                color: #FFFFFF;
                border: none;
                border-radius: 6px;
                padding: 8px 14px;
                font-weight: bold;
            }
            QPushButton:hover {
                background-color: #106EBE;
            }
            QPushButton:pressed {
                background-color: #005A9E;
            }
            QPushButton:disabled,
            QLineEdit:disabled {
                background-color: #E5E5E5;
                color: #888888;
                border: 1px solid #C8C8C8;
            }
            """
        )
        
        # 2. Conectăm butonul din design la funcția de citire
        # "buton_conectare" și "label_valoare" sunt numele (objectName) setate de tine în Qt Designer
        self.ui.btn_conectare.clicked.connect(self.porneste_citirea)
        self.ui.btn_stop.clicked.connect(self.opreste_citirea)
        
        # Variabilă pentru a controla bucla de citire
        self.citire_activa = False

    @asyncSlot()
    async def porneste_citirea(self):
        if self.citire_activa:
            return  # Prevenim rularea multiplă
        self.citire_activa = True
        self.ui.btn_conectare.setEnabled(False) # Dezactivăm butonul cât timp citim
        self.ui.btn_stop.setEnabled(True)
        self.ui.label_valoare.setText("Se conectează...")

        # IP-ul PLC-ului tău
        plc_address = self.ui.pln_text_PLC_addres.toPlainText().strip()
        plc_port = self.ui.pln_text_PLC_port.toPlainText().strip()
        url_plc = "opc.tcp://"+plc_address+":"+plc_port

        client = Client(url=url_plc,timeout=10)
        try:
            async with client:
                self.ui.label_valoare.setText("Conectat! Se citesc datele...")
                # Pune aici NodeID-ul aflat cu UaExpert
                nod_variabila = client.get_node("ns=4;i=68")

                # În bucla de citire:
                valori_array = await nod_variabila.read_value()

                # valori_array este o listă Python, ex: [11, 114, 0, 0, 5, ...]
                while self.citire_activa:
                    valoare = await nod_variabila.read_value()
                    
                    # Actualizăm textul în interfața desenată de tine
                    self.ui.txt_recirculare.setText(str(valoare[0]))
                    self.ui.txt_centrala_gaz.setText(str(valoare[1]))
                    self.ui.txt_pompa_puffer.setText(str(valoare[2]))
                    self.ui.txt_pompa_caldura.setText(str(valoare[3]))

                    self.ui.txt_recirculare_lastday.setText(str(valoare[4]))
                    self.ui.txt_centrala_gaz_lastday.setText(str(valoare[5]))
                    self.ui.txt_pompa_puffer_lastday.setText(str(valoare[6]))
                    self.ui.txt_pompa_caldura_lastday.setText(str(valoare[7]))

                    self.ui.txt_incalzire_sub.setText(str(valoare[8]))
                    self.ui.txt_incalzire_parter.setText(str(valoare[9]))
                    self.ui.txt_incalzire_etaj.setText(str(valoare[10]))
                    self.ui.txt_incalzire_pod.setText(str(valoare[11]))

                    self.ui.txt_incalzire_sub_lastday.setText(str(valoare[12]))
                    self.ui.txt_incalzire_parter_lastday.setText(str(valoare[13]))
                    self.ui.txt_incalzire_etaj_lastday.setText(str(valoare[14]))
                    self.ui.txt_incalzire_pod_lastday.setText(str(valoare[15]))

                    self.ui.txt_recirculare_next.setText(str(valoare[16]))
                    
                    await asyncio.sleep(1) # Pauză 1 secundă
                    
        except Exception as e:
            self.ui.label_valoare.setText(f"Eroare: {e}")
            print(f"Detalii eroare:{e}")
        finally:
            self.citire_activa = False
            self.ui.btn_conectare.setEnabled(True)

    def opreste_citirea(self):
        #Această funcție oprește bucla de citire
        self.citire_activa = False
        self.ui.label_valoare.setText("Deconectat")
        self.ui.btn_stop.setEnabled(False)

if __name__ == "__main__":
    # Inițializăm aplicația Qt împreună cu bucla asincronă (necesară pentru OPC UA)
    app = QApplication(sys.argv)
    loop = QEventLoop(app)
    asyncio.set_event_loop(loop)
    
    fereastra = FereastraPrincipala()
    fereastra.show()
    
    with loop:
        loop.run_forever()