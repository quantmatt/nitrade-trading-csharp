# -*- coding: utf-8 -*-

def load_bars(assets, verbose=True):
    
    import pandas as pd
    
    if type(assets) == tuple:
        assets = [assets]
        
    data = []
    
    for asset, asset_file in assets:
        
        dataset = pd.DataFrame()
        
        if verbose: print("Loading " + asset_file)
        
        dataset = pd.read_csv(asset_file, index_col=0, parse_dates=True)

        dataset['open'] = dataset['askopen']
        dataset['close'] = dataset['askclose']
        dataset['high'] = dataset['askhigh']
        dataset['low'] = dataset['asklow']
        dataset['volume'] = dataset['tickqty']
        dataset = dataset.drop(['askopen', 'askclose','askhigh','asklow','tickqty'], axis=1)
            
        data.append((asset, dataset))
    
    if len(data) == 1:
        return data[0]
    return data

def compile_from_minute_data(assets, asset_file = None, timeframe = None, save_path=None, verbose=True):
    """ Compiles higher level OHLC bars from minute data
    
    Args:
        assets (str or str[]): a single asset name or array of asset names
        timeframe (str): the time frame of the bar eg. 1H is 1 hour bars
        asset_file (str): Path of the minute data with a placeholder {ASSET} that will
            be substituted with the asset name
        save_path (str): Path to save the bar data with a placeholder {ASSET} that will
            be substituted with the asset name
        verbose (bool): True if progress printing to console is desired
            
    Returns:
        pd.DataFrame:  a single tuple of (asset name, dataframe) of the 
            bar data if a single asset was passed or an array of (asset name, dataframes)
            if an array of assets was passed
    
    """
    import pandas as pd
    
    #if a single asset is passed just put it in a single item list
    if type(assets) == str or type(assets) == tuple:
        assets = [assets]
    elif type(assets) != list:
        raise ValueError('assets must be a string or a list of strings.')
        
    asset_data_list = []
        
    #create the bars for every asset in the assets array
    for asset in assets:   
        
        if type(asset) == str:
        
            if verbose: print("Reading {0}...".format(asset))
            
            asset = asset.replace("/", "")
            
            file_location = asset_file.replace("{ASSET}", asset)
            print(file_location)
            #get the minute data
            dataset = pd.read_csv(file_location, index_col=0, parse_dates=True)
            
            #Create the OHLC values based on Ask price - this is inline with Zorro 
            #Trading Platform that also uses Ask price
            #the Index time value is the time at the start of the bar so all this 
            #information is known only after the bar has closed

            dataset['open'] = dataset['askopen']
            dataset['close'] = dataset['askclose']
            dataset['high'] = dataset['askhigh']
            dataset['low'] = dataset['asklow']
            dataset['spread_close'] = dataset['askclose'] - dataset['bidclose']
            dataset['spread_open'] = dataset['askopen'] - dataset['bidopen']
            dataset['spread_max'] = dataset['askhigh'] - dataset['bidlow']
            dataset['volume'] = dataset['tickqty']
       
        #tuple of asset name and dataFrame
        else:            
            dataset = asset[1]
            asset = asset[0]        
        
        if timeframe is not None:
            #resample the minute data into the new timeframe
            #conversion = {'open': 'first', 'high': 'max', 'low' : 'min', 'close': 'last', 'volume' : 'sum', 'spread_open': 'first', 'spread_close': 'last', 'spread_max' : 'max'}        
            conversion = {'bidopen': 'first', 'bidclose': 'last', 'bidhigh': 'max', 'bidlow' : 'min', 
                          'open': 'first', 'close': 'last', 'high': 'max', 'low' : 'min',                           
                          'volume' : 'sum'}        
            bars = dataset.resample(timeframe).agg(conversion)
            
            #get rid of the weekend bars and bars with no data
            bars = bars.dropna()
            
            #get some info about the open of the next bar because it may be
            #at the other side of the weekend and this is when a trade will
            #be opened based on info  from the last complete bar    
            bars["temp"] = bars.index
            bars["next_bar_open"] = bars["temp"].shift(-1)
            bars = bars.drop("temp", axis=1)
            #the actual price a trade will be opened will be the first price of the
            #next bar so store this for convenience
            bars["next_askopen"] = bars["open"].shift(-1)
            bars["next_bidopen"] = bars["bidopen"].shift(-1) #- bars["spread_open"].shift(-1)
            
            if save_path is not None:
                if verbose: print("Saving {0}...".format(asset))
                save_location = save_path.replace("{ASSET}", asset)
                bars.to_csv(save_location)
                
            asset_data_list.append((asset, bars))
        else:
            asset_data_list.append((asset, dataset))
        
    #if there was just one asset just return a single dataframe, otherwise 
    #return the list of dataframes, one for each asset
    if len(asset_data_list) == 1:
        return asset_data_list[0]
    else:
        return asset_data_list
    
def bars_from_file(assets, asset_file, verbose=True):    
    """ Reads OHLC bars from file
    
    Args:
        assets (str or str[]): a single asset name or array of asset names        
        asset_file (str): Path of the minute data with a placeholder {ASSET} that will
            be substituted with the asset name   
        verbose (bool): True if progress printing to console is desired
            
    Returns:
        pd.DataFrame:  a single tuple of (asset name, dataframe) of the 
            bar data if a single asset was passed or an array of (asset name, dataframes)
            if an array of assets was passed
    
    """
    
    import pandas as pd
    
    #if a single asset is passed just put it in a single item list
    if type(assets) == str:
        assets = [assets]
    elif type(assets) != list:
        raise ValueError('assets must be a string or a list of strings.')
        
    asset_data_list = []
    
    #create the bars for every asset in the assets array
    for asset in assets:   
        if verbose: print("Reading {0}...".format(asset))
        
        file_location = asset_file.replace("{ASSET}", asset)
        
        #get the bar data
        dataset = pd.read_csv(file_location, index_col=0, parse_dates=True)
        
        asset_data_list.append((asset, dataset))
    
    #if there was just one asset just return a single dataframe, otherwise 
    #return the list of dataframes, one for each asset
    if len(asset_data_list) == 1:
        return asset_data_list[0]
    else:
        return asset_data_list
    


    
def add_features(asset_data, features = None, save_path=None, verbose=True):
    """ Adds features to the bar data. If no features are passed then all features
    are added.
    
    Args:
        asset_data ((str, pd.DataFrame) or (str, pd.DataFrame)[]): a single 
        or list of tuples (asset name, bar data) containing the bar data
        features (str[]): a list of features to include, all features if this 
            is None
        save_path (str): Path to save the bar data with features. A 
        placeholder {ASSET} that will be substituted with the asset name
        verbose (bool): True if progress printing to console is desired
        
    Returns:
        (str, pd.DataFrame): a single tuple of (asset name, dataframe) of the 
            bar data if a single asset was passed or an array of (asset name, dataframes)
            if an array of assets was passed
    
    """
    
    #helper function for creating logs
    def replace_zero_with_min(series):
        return series.replace(0, series.loc[series > 0].min())  

    import pandas as pd
    import numpy as np
    from pyti import bollinger_bands as bbands
    from pyti import average_true_range as atr
    
    #if a single dataframe is passed just put it in a single item list
    if type(asset_data) == tuple:
        asset_data = [asset_data]
    elif type(asset_data) != list:
        raise ValueError('asset_data must be a pandas.DataFrame or a list of pandas.Dataframe.')

    feature_bars = []
        
    for asset, data in asset_data:
        
        if verbose: print("Calculating features for {0}".format(asset))
        
        #Lower Bollinger Band, 20 periods, std = 2 
        if features == None or 'lower_bb' in features: 
            data['lower_bb'] = bbands.lower_bollinger_band(data["close"], 20, std=2.0)
            
        #Upper Bollinger Band, 20 periods, std = 2 
        if features == None or 'upper_bb' in features: 
            data['upper_bb'] = bbands.upper_bollinger_band(data["close"], 20, std_mult=2.0)
            
        #Average True Range
        if features == None or 'atr' in features: 
                data["atr"] = atr.average_true_range(data["close"], 24) / \
                    atr.average_true_range(data["close"], 200)
        
        #Volatility is the standard deviation over 12 periods of the difference
        #between high and low of the bar
        if features == None or 'volatility_12' in features: 
            data["volatility_12"] = (data["high"] - data["low"]).rolling(12).std() 
            
        #Volatility is the standard deviation over 200 periods of the difference
        #between high and low of the bar
        if features == None or 'volatility_200' in features: 
            data["volatility_200"] = (data["high"] - data["low"]).rolling(200).std() 
            
        #Volatility is relative change of the alst 12 bars over the last 200
        if features == None or 'volatility' in features: 
            data["volatility"] = data["volatility_12"] / data["volatility_200"]
            
        #Relative volume compared to the last 100 bars
        if features == None or 'volume_change' in features: 
            data["volume_change"] = data["volume"] / data["volume"].rolling(100).mean()
            
        #Distance between the close price and the upper bollinger bad
        if features == None or 'bb_dist_upper' in features: 
            data["bb_dist_upper"] = data["upper_bb"] - data["close"]
            
        #Distance between the close price and the lower bollinger bad
        if features == None or 'bb_dist_lower' in features: 
            data["bb_dist_lower"] = -(data["lower_bb"] - data["close"])
            
            
        #Distance between the upper and lower bollinger bands
        if features == None or 'bb_range' in features: 
            data["bb_range"] = (data["upper_bb"] - data["lower_bb"]) / data["close"]
            
        #The absolute value of the % return over the last 4 bars
        if features == None or 'change_4bar' in features: 
            data["change_4bar"] = np.abs(np.log(data["close"] / data["close"].shift(4)))
    
        #The Augmented Dicker Fuller test which can show mean reverting or trending markets
        #from statsmodels.tsa.stattools import adfuller  
        #if features == None or 'adf' in features: 
        #    data["adf"] = data['Close'].rolling(200).apply(lambda x: adfuller(x)[0], raw=False)
        
        #The log of the % return
        if features == None or 'log_return' in features: 
            data["log_return"] = np.log(data["close"] / data["close"].shift(1))
            

        #TODO add in the if feature statements
        #add logs - do this by replacing all zeros with the minimum value after zeros are removed
        if 1 == 0:
            data["volume_log"] = np.log(replace_zero_with_min(data["volume"])) / np.log(replace_zero_with_min(data["volume"].rolling(200).mean()))
            data["atr_log"] = np.log(replace_zero_with_min(data["atr"]))
            data["volatility_log"] = np.log(replace_zero_with_min(data["volatility"]))
            data["change_4bar_log"] = np.log(replace_zero_with_min(data["change_4bar"]))
            
            #add moving averages
            data["volume_log_ma_12"] = data["volume_log"].rolling(12).mean()
            data["volume_log_ma_24"] = data["volume_log"].rolling(24).mean()
            data["volume_log_ma_48"] = data["volume_log"].rolling(48).mean()
            data["volatility_log_ma_12"] = data["volatility_log"].rolling(12).mean()
            data["volatility_log_ma_24"] = data["volatility_log"].rolling(24).mean()
            data["volatility_log_ma_48"] = data["volatility_log"].rolling(48).mean()
            data["change_4bar_log_ma_12"] = data["change_4bar_log"].rolling(12).mean()
            data["change_4bar_log_ma_24"] = data["change_4bar_log"].rolling(24).mean()
            data["change_4bar_log_ma_48"] = data["change_4bar_log"].rolling(48).mean()
            data["log_return_ma_12"] = data["log_return"].rolling(12).mean()
            data["log_return_ma_24"] = data["log_return"].rolling(24).mean()
            data["log_return_ma_48"] = data["log_return"].rolling(48).mean()
        
        feature_bars.append((asset, data))
        
        if save_path is not None:
            if verbose: print("Saving {0}...".format(asset))
            save_location = save_path.replace("{ASSET}", asset)
            data.to_csv(save_location)
        
    #if there was just one dataFrame just return a single dataframe, otherwise 
    #return the list of dataframes, one for each asset
    if len(feature_bars) == 1:
        return feature_bars[0]
    else:
        return feature_bars

def calc_feature(data, feature, args):
    
    #helper function for creating logs
    def replace_zero_with_min(series):
        return series.replace(0, series.loc[series > 0].min())  

    import numpy as np
    import pandas as pd
    from pyti import bollinger_bands as bbands
    
    
    if feature == "SMA": 
        if len(args) != 2:
            raise ValueError("SMA requires 2 args; period and column name")
            
        return data[args[1]].rolling(int(args[0])).mean()
    
    elif feature == "BBANDS":
        if len(args) != 4:
            raise ValueError("BBANDS requires 4 args; period, std dev, data_type (1=upper, 2=lower, 3=middle, 4=range) and column name")
        if(int(args[2]) == 1):
            return bbands.upper_bollinger_band(data[args[3]], int(args[0]), float(args[1]))
        elif(int(args[2]) == 2):
            return bbands.lower_bollinger_band(data[args[3]], int(args[0]), float(args[1]))
        elif(int(args[2]) == 3):
            return bbands.middle_bollinger_band(data[args[3]], int(args[0]), float(args[1]))
        elif(int(args[2]) == 4):
            return bbands.bb_range(data[args[3]], int(args[0]), float(args[1]))
        else:
             raise ValueError("BBANDS data_type must be one of (1=upper, 2=lower, 3=middle, 4=range)")
             
    elif feature == "ATR":
        if len(args) != 4:
            raise ValueError("ATR requires 4 args; period and close column, high column, low column")        
        
        range_table = pd.DataFrame()
        range_table["A"] = data[args[2]]-data[args[3]]
        range_table["B"] = abs(data[args[2]]-data[args[1]].shift(1))
        range_table["C"]=  abs(data[args[3]]-data[args[1]].shift(1))
        range_table["max"] = range_table[['A', 'B', 'C']].max(axis=1)
        

        return range_table["max"].rolling(int(args[0])).mean()
    
    elif feature == "VOLATILITY_LOG_MA":
        #calculates relative volatility of 12 periods compared to 200 the
        #smooths with a moving average
        if len(args) != 3:
            raise ValueError("ATR requires 3 args; period, high column name, low column name")   
        data["volatility_12"] = (data[args[1]] - data[args[2]]).rolling(12).std() 
        data["volatility_200"] = (data[args[1]] - data[args[2]]).rolling(200).std() 
        data["volatility"] = data["volatility_12"] / data["volatility_200"]
        data["volatility_log"] = np.log(replace_zero_with_min(data["volatility"]))   
        return data["volatility_log"].rolling(int(args[0])).mean()
    
    elif feature == "VOLUME_LOG_MA":
        if len(args) != 2:
            raise ValueError("VOLUME_LOG_MA requires 2 args; period, column name")   
        data["volume_log"] = np.log(replace_zero_with_min(data[args[1]])) / np.log(replace_zero_with_min(data[args[1]].rolling(200).mean()))
        return data["volume_log"].rolling(int(args[0])).mean()
  
    
def add_additional_features(asset_data, features, save_path=None, verbose=True):
    """ Can add in more features to the data if already processed, saves 
        calculating all features again
        
        Args:
            asset_data ((str, pd.DataFrame) or (str, pd.DataFrame)[]): a single 
                or list of tuples (asset name, bar data) containing the bar data
            features (str[]): a string list of features to calculate
            save_path (str): Path to save the bar data with features. A 
                placeholder {ASSET} that will be substituted with the asset name
            verbose (bool): True if progress printing to console is desired
            
        
        Returns:
        (str, pd.DataFrame): a single tuple of (asset name, dataframe) of the 
            bar data if a single asset was passed or an array of (asset name, dataframes)
            if an array of assets was passed
    
    """
    import pandas as pd
    
    #if a single dataframe is passed just put it in a single item list
    if type(asset_data) == tuple:
        asset_data = [asset_data]
    elif type(asset_data) != list:
        raise ValueError('asset_data must be a (str, pandas.DataFrame) or a list of (str, pandas.Dataframe).')

    
    feature_bars = []
        
    for asset, data in asset_data:
        
        #add a new feature - this line is temporary and can be change to 
        #whatever calcualting is required
        data["feature"] = 0
    
        feature_bars.append((asset, data))
        
        if save_path is not None:
            if verbose: print("Saving {0}...".format(asset))
            save_location = save_path.replace("{ASSET}", asset)
            data.to_csv(save_location)
    
    #if there was just one dataFrame just return a single dataframe, otherwise 
    #return the list of dataframes, one for each asset
    if len(feature_bars) == 1:
        return feature_bars[0]
    else:
        return feature_bars
    
    
def merge_trades(asset_data, trade_path):
    """ Loads the trades csv from Zorro and merges the data with bar data 
        that is know at the time of placing the trade
        
        Args:
            asset_data ((str, pd.DataFrame) or (str, pd.DataFrame)[]): a single 
                or list of tuples (asset name, bar data) containing the bar data
            trade_path (str): Path where the trade results are stored. A 
                placeholder {ASSET} that will be substituted with the asset name. 
                If no placeholder is in the string then the filename will be loaded
                as is.
                
        Returns:
            pd.DataFrame: all the trades with the known features at the time of
                placing the trades
    """
    import pandas as pd
    import numpy as np
    
    trade_sets = []
    
     #if a single dataframe is passed just put it in a single item list
    if type(asset_data) == tuple:
        asset_data = [asset_data]
    elif type(asset_data) != list:
        raise ValueError('asset_data must be a (str, pandas.DataFrame) or a list of (str, pandas.Dataframe).')

    
    #If no placeholder then load all trades from the one file
    if "{ASSET}" not in trade_path:
        all_trades = pd.read_csv(trade_path, parse_dates=True)
    
    for asset, data in asset_data:
        
        #If trades are in separate asset files load from these files
        if "{ASSET}" in trade_path:            
            trade_location = trade_path.replace("{ASSET}", asset)
            trades = pd.read_csv(trade_location, parse_dates=True)
        else:
            trades = all_trades.loc[all_trades["Asset"].str.contains(asset)]
        
        #Update the Open and Close time of the trade so the column names
        #don't conflict with the bar columns
        trades["Entry Time"] = pd.to_datetime(trades["open"])
        trades["Exit Time"] = pd.to_datetime(trades["close"])
        trades = trades.drop(["open", "close"], axis=1)
        
        #make sure the bar data is in datetime format
        data["next_bar_open"] = pd.to_datetime(data["next_bar_open"])
        
        #merge the trade data with data that was known at the time the trade was open
        #bar data will have a next bar open attribute which is the open time of the next bar
        #this is when a trade is exectuted after knowing all data from the close of the bar
        joined = pd.merge(trades, data, how="left", left_on="Entry Time", right_on="next_bar_open")
        joined = joined.sort_values("next_bar_open", axis=0)
        
        #mark a trade as win or lose
        joined["win"] = np.where(joined["Profit"] > 0, 1, 0)
        
        #add some time based features
        joined["dow"] = joined["Entry Time"].dt.dayofweek
        joined["hour"] = joined["Entry Time"].dt.hour
        
        #add some direction based features
        joined["bb_dist_upper"] = np.where(joined["Type"] == 'Short', -joined["bb_dist_upper"],  joined["bb_dist_upper"])
        joined["bb_dist_lower"] = np.where(joined["Type"] == 'Short', joined["bb_dist_lower"],  -joined["bb_dist_lower"])
        joined["log_return_direction"] = np.where(joined["Type"] == 'Short', joined["log_return"],  -joined["log_return"])
        joined["log_return_direction_12"] = np.where(joined["Type"] == 'Short', joined["log_return_ma_12"],  -joined["log_return_ma_12"])
        joined["log_return_direction_24"] = np.where(joined["Type"] == 'Short', joined["log_return_ma_24"],  -joined["log_return_ma_24"])
        joined["log_return_direction_48"] = np.where(joined["Type"] == 'Short', joined["log_return_ma_48"],  -joined["log_return_ma_48"])
        
        
        trade_sets.append(joined)
        
    all_joined = pd.concat(trade_sets)
    
    all_joined.index = pd.to_datetime(all_joined["Entry Time"])
    
    return all_joined
    

def prep_zorro_import(asset_data, save_path, 
                      market_vol_val_timeframe = None, 
                      market_vol_field = None,
                      market_val_field = None,
                      bar_time_min = 1):
    """ Prepares the data for zorro by sorting in the correct direction and
    setting the timestamp to the close of the bar 
    
    Args:
        asset_data (pd.DataFrame): time based bar data for a single asset
        save_path (str):  Path where the data will be saved.
        bar_time_min (int): the length of the bar in minutes
        market_vol_val_timeframe (str): time frame to aggregate the vol and val
            numbers because zorro doesn't do this, it only takes the value on the
            start of the bar
        market_vol_field (str): the dataframe column to get the vol data
        market_val_field (str): the dataframe column to get the val data
    
    """
    
    from datetime import timedelta
    
    #change the time to the end of the bar because this is what Zorro expects
    #NOTE: This is not the case for daily bars
    asset_data.index = asset_data.index + timedelta(minutes=bar_time_min)
    
    #aggregate the marklet vol and val fields based on the passed timeframe
    #This is because zorro doesn't do this in bar building of historic data
    if market_vol_val_timeframe is not None:        
        units  = market_vol_val_timeframe[-1:]
        val = int(market_vol_val_timeframe[:-1])
    
        #This has been painstakingly checked to make sure the bars
        #built by zorro have teh correct aggregated volume and vals and 
        #timestamps are correct
        if market_vol_field is not None:
            asset_data["Volume"] = asset_data[market_vol_field].shift(val, freq=units).rolling(market_vol_val_timeframe).sum().shift(-val, freq=units)
        if market_val_field is not None:
            asset_data["Val"] = asset_data[market_val_field].shift(val, freq=units).rolling(market_vol_val_timeframe).sum().shift(-val, freq=units)
    
    #reverse the order of the data because this is how zorro requires it
    asset_data = asset_data.sort_index(ascending=False)
    
    #save to csv
    header = ['Open', 'high', 'Low', 'Close', 'Volume', 'Val']
    asset_data.to_csv(save_path, columns = header)
    
    
    
def filter_trades(trades, filter_definitions):
    """ Will accept a trade that fits any of the filters ie OR not AND
    """
    
    import pandas as pd
    
    filtered = pd.DataFrame()
    
    for feature, filter_def_list in filter_definitions:
        for low_cut, high_cut in filter_def_list:                       
            
            filtered = filtered.append(trades.loc[(trades[feature] >= low_cut) &
                                                  (trades[feature] <= high_cut)])
            
    return filtered
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    