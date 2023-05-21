import json
import os

PATH = '/home/runner/work/'

swinfo_amount = 0
swinfo_path = ""

class ResolutionError(Exception):
    pass

for root, subFolder, files in os.walk(PATH):
    for item in files:
        if item == "swinfo.json" :
            swinfo_amount += 1
            if swinfo_amount > 1:
                raise ResolutionError("You have more than 1 'swinfo.info' file in your project, please resolve this")
            swinfo_path = os.path.join(root, item)

print(swinfo_path)
    
            
