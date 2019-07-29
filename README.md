# NeoBlock-Mongo-Storage
[简体中文](#zh) |    [English](#en) 

<a name="zh">简体中文</a>
## 概述 :
- 本项目的功能是爬取节点的数据，对数据初步解析并存入数据库。
- 从节点获取到块数据，并解析出块数据中的交易数据，utxo，每个块的系统费，资产详情等信息。
- 部分入库数据，如notify，nep5余额等数据的入库迁移到 _[neo-cli-nel](https://github.com/NewEconoLab/neo-cli-nel)_ 中。两个工程配合能更好的爬取数据并存入数据库。


## 部署演示 :

安装git（如果已经安装则跳过） :
```
yum install git -y
```

安装 dotnet sdk :
```
rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
yum update
yum install libunwind libicu -y
yum install dotnet-sdk-2.1.200 -y
```

通过git将本工程下载到服务器 :
```
git clone https://github.com/NewEconoLab/NeoBlock-Mongo-Storage.git
```

修改配置文件放在执行文件下，配置文件大致如下 :
```json
{
  "mongodbConnStr": "",
  "mongodbDatabase": "",
  "NeoCliJsonRPCUrl": "",
  "sleepTime": ,
  "utxoIsSleep": ,
  "isDoNotify": ,
  "isDoFullLogs": ,
  "cliType": ""
}
```


编译并运行
```
dotnet publish
cd  NeoBlock-Mongo-Storage/NeoBlockMongoStorage/NeoBlockMongoStorage/bin/Debug/netcoreapp2.0
dotnet NEL_Scan_API.dll
```


<a name="en">English</a>
## Overview :
- The function of this project is to crawl the data of the node, parse the data and store it in the database.
- Obtain block data from the node, and parse the transaction data in the block data, utxo, system fee per block, asset details and other information.
- Some inbound data, such as notify, nep5 balance, etc., are migrated to _[neo-cli-nel](https://github.com/NewEconoLab/neo-cli-nel)_. Two engineering collaborations can better crawl data and store it in the database.

## Deployment

install git（Skip if already installed） :
```
yum install git -y
```

install dotnet sdk :
```
rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
yum update
yum install libunwind libicu -y
yum install dotnet-sdk-2.1.200 -y
```

clone to the server :
```
git clone https://github.com/NewEconoLab/NeoBlock-Mongo-Storage.git
```

Modify the configuration file under the execution file, the configuration file is roughly as follows:
```json
{
  "mongodbConnStr": "",
  "mongodbDatabase": "",
  "NeoCliJsonRPCUrl": "",
  "sleepTime": ,
  "utxoIsSleep": ,
  "isDoNotify": ,
  "isDoFullLogs": ,
  "cliType": ""
}
```

Compile and run :
```
dotnet publish
cd  NeoBlock-Mongo-Storage/NeoBlockMongoStorage/NeoBlockMongoStorage/bin/Debug/netcoreapp2.0
dotnet NEL_Scan_API.dll
```
