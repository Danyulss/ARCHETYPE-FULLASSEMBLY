import _wmi
import logging

def temp_check():
    w = _wmi.WMI(namespace="root/OpenHardwareMonitor")
    temperature_infos = w.Sensor()
    for sensor in temperature_infos:
        if sensor.SensorType==u'Temperature':
                logging.info("current GPU temp "  + sensor.Value)