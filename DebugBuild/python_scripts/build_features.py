# -*- coding: utf-8 -*-
"""
Created on Sat Mar 30 17:26:10 2019

@author: matth
"""

import numpy as np
from numpy import dtype
import data_builder as db
import pandas as pd

#get the datafeed type ie. all or single
df_type = input()
#get the filename for the binary data
filename = input()
#get the filename for the binary data output - if passed as a number then this is 
#returned as a string through stdout instead of writing to file
output = input()
#get the requested features as function(arg1, arg2);
line = input()


#uncomment for easier testing in python
#df_type = "whole"
#filename = "C:\\ForexData\\ShareData\\EURUSD_m60_Share_live.bin"
#output = "212"
#line = "SMA(20,close);ATR(3,close,high,low);ATR(4,close,high,low);ATR(5,close,high,low);ATR(100,close,high,low);VOLATILITY_LOG_MA(12,high,low);VOLUME_LOG_MA(12,volume);BBANDS(20,1.8,1,close);BBANDS(20,1.8,2,close)"




features = line.split(';')
#strip out any excess blanks
features = [x for x in features if len(x) > 0]

#load the binary data which will just be the date and the value used to calculate
#the features - dont need all OHLC data
if(df_type == "single"):
    dt =  dtype((np.record, [('date', '<M8[ns]'), ('value', '<f4')]))
else:
    dt =  dtype((np.record, [('date', '<M8[ns]'), 
                             ('open', '<f4'), 
                             ('close', '<f4'), 
                             ('high', '<f4'), 
                             ('low', '<f4'), 
                             ('volume', '<i4')]))
    
records = np.fromfile(filename, dt)
data = pd.DataFrame(records)
#need to sort because the binary file might not be in order due to the use of a dictionary
data.index = data["date"]
data = data.drop('date', axis=1)
data = data.sort_index() 


data["close"] = data["open"]

#create a dataframe to hold the values of each feature
results_data = pd.DataFrame()

#loop through all the requested features and do the calculations
for feature_call in features:
    
    #extract the required parts from the feature call ie. name and args
    feature_name = feature_call[:feature_call.index("(")].strip()
    feature_arg_string = feature_call[feature_call.index("(")+1:feature_call.index(")")]
    feature_arg_string = feature_arg_string.replace(" ", "")
    feature_args = feature_arg_string.split(',')   
    
    #calculate the indicator and add it to the returning dataframe
    column_name = feature_call.replace(',', '_').replace('(','_').replace(')','').replace(' ', '')
    results_data[column_name] = db.calc_feature(data, feature_name, feature_args)
    results_data.index = data.index

#return in the std ouput if output is specified as the number of bars
if output.isdigit():
    #clip to just the required number of bars and reverse the order so it 
    #is as a series ie. newest data first    
    reversed_data = results_data.reindex(index=results_data.index[::-1])
    clipped_data = reversed_data.iloc[:int(output)]
    print(clipped_data.to_csv(header=False))
    
else:    
    #write all the dataframe back to the binary file with overwrite 
    new_recarray = results_data.to_records()
    new_recarray.tofile(output)
    print("Success")    

