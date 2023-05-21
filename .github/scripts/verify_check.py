import json
import os

PATH = '/home/runner/work/'

swinfo_amount = 0

for root, subFolder, files in os.walk(PATH):
    for item in files:
        if item == "swinfo.json" :
            swinfo_amount += 1

print(swinfo_amount)