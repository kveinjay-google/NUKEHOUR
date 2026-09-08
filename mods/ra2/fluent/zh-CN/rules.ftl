## Player
options-tech-level =
    .infantry-only = 仅步兵
    .low = 低
    .medium = 中
    .unrestricted = 无限制

checkbox-redeployable-mcvs =
    .label = 基地车可重新收起
    .description = 允许建造厂重新收起为基地车

## World
options-starting-units =
    .mcv-only = 仅基地车
    .light = 轻型
    .medium = 中型
    .heavy = 重型

## ai.yaml
actor-player-modularbot-testai-name = 测试 AI

## aircraft.yaml
actor-shad =
    .name = 夜鹰直升机
    .description =
    步兵运输直升机，
    雷达无法侦测。
      擅长对抗步兵
      不擅长对抗载具、飞机

actor-shadhusk-name = 夜鹰直升机
actor-zep-name = 基洛夫空艇
actor-zephusk-name = 基洛夫空艇

actor-orca =
    .name = 入侵者战机
    .description =
    高速突击战机
      擅长对抗建筑、载具
      不擅长对抗步兵、飞机

actor-orcahusk-name = 入侵者战机
actor-beag-name = 黑鹰战机
actor-beaghusk-name = 黑鹰战机
actor-pdplane-name = 运输机
actor-pdplanehusk-name = 运输机
actor-hornet-name = 大黄蜂
actor-hornethusk-name = 大黄蜂
actor-asw-name = 鱼鹰

actor-aswhusk =
    .name = 鱼鹰
    .norow--name = 鱼鹰

## allied-infantry.yaml
actor-engineer =
    .description =
    占领敌方建筑。
      无武装
    .name = 工程师

actor-dog =
    .description =
    反步兵单位，
    可侦测隐形单位与间谍。
      擅长对抗步兵
      不擅长对抗载具、飞机
    .name = 警犬

actor-e1 =
    .description =
    多用途步兵。
      擅长对抗步兵
      不擅长对抗载具、飞机
    .name = 美国大兵

actor-snipe =
    .name = 狙击手
    .description =
    特种反步兵单位。
      擅长对抗步兵
      不擅长对抗载具、飞机

actor-spy =
    .description =
    潜入敌方建筑窃取情报或
    进行破坏，具体效果取决于
    潜入的建筑类型。
      无武装
    特殊能力：伪装
    .disguisetooltip-name = 间谍
    .disguisetooltip-generic-name = 士兵

actor-ghost =
    .description =
    精英突击步兵，配备
    冲锋枪与 C4 炸药。
      擅长对抗步兵、建筑
      不擅长对抗载具、飞机
    特殊能力：用 C4 摧毁建筑
    .name = 海豹部队

actor-ccomand =
    .description =
    精英突击步兵，配备
    冲锋枪与 C4 炸药。
      擅长对抗步兵、建筑
      不擅长对抗载具、飞机
    特殊能力：用 C4 摧毁建筑
    .name = 超时空突击队

actor-ptroop =
    .description =
    心灵步兵，可心灵控制敌方单位。
      擅长对抗步兵、载具、建筑
      不擅长对抗警犬、恐怖机器人、飞机
    特殊能力：用 C4 摧毁建筑
    .name = 心灵突击队

actor-tany =
    .description =
    精英突击步兵，配备
    双枪与 C4 炸药。
      擅长对抗步兵、建筑
      不擅长对抗载具、飞机
    特殊能力：用 C4 摧毁建筑

    最多可训练 1 名。
    .name = 谭雅

actor-jumpjet =
    .description =
    空降士兵。
      擅长对抗步兵、飞机
      不擅长对抗载具
    .name = 火箭飞行兵

actor-jumpjet-husk-name = 火箭飞行兵

actor-cleg =
    .name = 超时空军团兵
    .description =
    高科技士兵。
     擅长对抗步兵、载具
     不擅长对抗飞机

## allied-naval.yaml
actor-lcrf =
    .description =
    多用途海上运输，
    可运载步兵与载具。
      无武装
    .name = 两栖运输艇

actor-dest =
    .description =
    盟军主力战舰，配备舰炮与
    鱼鹰直升机，
    可侦测潜艇与海洋生物。
      擅长对抗海军单位
      不擅长对抗地面单位、飞机
    .name = 驱逐舰

actor-aegis =
    .description =
    防空海军单位。
      擅长对抗飞机
      不擅长对抗地面单位、舰船
    .name = 神盾巡洋舰

actor-dlph =
    .description =
    训练有素的海豚，
    配备声波武器。
      擅长对抗舰船
    .name = 海豚

actor-carrier =
    .description =
    航空母舰。
      擅长对抗坦克、建筑
      不擅长对抗步兵
    .name = 航空母舰

## allied-structures.yaml
actor-gacnst =
    .description = 建造基地建筑。
    .name = 建造厂

actor-gapowr =
    .description = 为其他建筑提供电力。
    .name = 发电厂

actor-gapile =
    .description = 训练步兵。
    .name = 兵营

actor-garefn =
    .description = 将矿石加工为资金。
    .name = 矿石精炼厂

actor-gaairc =
    .description =
    提供雷达，
    可停靠 4 架飞机。
    .name = 空军指挥部

actor-amradr =
    .name = 美国空军指挥部
    .paratrooperspower-paratroopers-name = 美国伞兵
    .paratrooperspower-paratroopers-description =
    一架运输机在地图任意位置
    空投 8 名美国大兵。

actor-gaweap =
    .description = 生产载具。
    .name = 战车工厂

actor-gayard =
    .name = 海军船坞
    .description =
    生产并维修舰船、
    潜艇、运输艇等海军单位。

actor-gadept =
    .description = 维修载具并移除恐怖机器人（收费）。
    .name = 维修厂

actor-gatech =
    .description = 解锁高级单位。
    .name = 作战实验室

actor-gawall =
    .description =
    轻型围墙，
    可被载具碾压。
    .name = 盟军围墙

actor-gapill =
    .description = 自动化反步兵防御。
    .name = 机枪碉堡

actor-nasam =
    .description = 自动化防空防御。
    .name = 爱国者导弹

actor-gtgcan =
    .description = 自动化远程反地面防御。
    .name = 巨炮

actor-gaorep =
    .description = 使所有来源的收入提升 25%。
    .name = 矿石精炼器

actor-gaspysat =
    .description = 揭开整个战场。
    .name = 间谍卫星

actor-gagap =
    .name = 裂缝产生器
    .description =
    用迷雾遮蔽敌人视野，
    需要电力才能运作。

actor-gaweat =
    .description = 像上帝一样操控致命天气！
    .name = 天气控制器
    .weathercontrolsupportpower-lightningstorm-name = 闪电风暴
    .weathercontrolsupportpower-lightningstorm-description = 操控天气摧毁敌军。

actor-gacsph =
    .description = 可传送 3x3 范围内的单位。
    .name = 超时空传送仪
    .chronoshiftpower-chronoshift-name = 超时空传送
    .chronoshiftpower-chronoshift-description =
    将一组单位传送至
    地图任意位置。

actor-atesla =
    .description =
    高级基地防御，
    需要电力才能运作。
      擅长对抗步兵、载具
      不擅长对抗飞机
    .name = 光棱塔

actor-power-name = 发电厂
actor-refinery-name = 矿石精炼厂
actor-barracks-name = 步兵生产
actor-radar-name = 雷达
actor-repairpad-name = 维修厂

## allied-vehicles.yaml
actor-amcv =
    .description = 可展开为建造厂。
    .name = 基地车

actor-cmin =
    .description =
    采集矿石。
      无武装
    特殊能力：可传送回己方精炼厂
    .name = 超时空采矿车

actor-mtnk =
    .name = 灰熊坦克
    .description =
    盟军主战坦克。
      擅长对抗载具、舰船
      不擅长对抗步兵、飞机

actor-tnkd =
    .name = 坦克杀手
    .description =
    特种反装甲单位。
      擅长对抗载具、舰船
      不擅长对抗步兵、飞机

actor-fv =
    .name = 多功能步兵车
    .description =
    多用途载具。
    无乘客时：
      擅长对抗步兵、飞机
      不擅长对抗载具、舰船
    特殊能力：武器随乘客变化。

actor-sref =
    .description =
    发射致命光束。
      擅长对抗步兵、载具
      不擅长对抗飞机
    .name = 光棱坦克

actor-mgtk =
    .description =
    伪装成树木的坦克。
      擅长对抗步兵、载具
      不擅长对抗飞机
    .name = 幻影坦克
    .miragetooltip-tree-name = 幻影坦克
    .miragetooltip-tree-generic-name = 树木

## animals.yaml
actor-cow-name = 奶牛
actor-all-name = 鳄鱼
actor-polarb-name = 北极熊
actor-josh-name = 猴子

## bridges.yaml
actor-cabhut-name = 桥梁维修小屋
meta-lowbridgeramp-name = 木桥
actor-lobrdb-a-name = 混凝土桥
actor-lobrdb-a-d-name = 断桥
actor-lobrdb-b-name = 混凝土桥
actor-lobrdb-b-d-name = 断桥
actor-lobrdb-r-se-name = 引桥
actor-lobrdb-r-nw-name = 引桥
actor-lobrdb-r-ne-name = 引桥
actor-lobrdb-r-sw-name = 引桥
actor-lobrdg-a-d-name = 断桥
actor-lobrdg-b-d-name = 断桥
actor-lobrdg-r-se-name = 引桥
actor-lobrdg-r-nw-name = 引桥
actor-lobrdg-r-ne-name = 引桥
actor-lobrdg-r-sw-name = 引桥
meta-elevatedbridgeplaceholder-name = 混凝土桥
actor-bridgb1-name = 木桥
actor-bridgb2-name = 木桥

## civilian-naval.yaml
actor-tug-name = 拖船
actor-cruise-name = 游轮
actor-cdest-name = 海岸巡逻艇

## civilian-props.yaml
actor-camisc01-name = 油桶
actor-camisc02-name = 油桶
actor-camisc03-name = 垃圾箱
actor-camisc04-name = 邮箱
actor-camisc05-name = 管道
actor-camisc06-name = V3 弹药
actor-camsc11-name = 轮胎
actor-camsc12-name = 练习靶
actor-camsc13-name = 废弃坦克
actor-ammocrat-name = 弹药箱
actor-camsc01-name = 热狗摊
actor-camsc02-name = 沙滩遮阳伞
actor-camsc03-name = 沙滩遮阳伞
actor-camsc04-name = 沙滩浴巾
actor-camsc05-name = 沙滩浴巾
actor-camsc06-name = 篝火
actor-caeuro05-name = 雕像
actor-capark01-name = 公园长椅
actor-capark02-name = 秋千
actor-capark03-name = 旋转木马
actor-castrt01-name = 红绿灯
actor-castrt02-name = 红绿灯
actor-castrt03-name = 红绿灯
actor-castrt04-name = 红绿灯
actor-castrt05-name = 公交站
actor-camov01-name = 汽车影院银幕
actor-camov02-name = 汽车影院小卖部
actor-pole01-name = 电线杆
actor-pole02-name = 电线杆
actor-hdstn01-name = 阿灵顿石碑
actor-spkr01-name = 汽车影院音箱
actor-cakrmw-name = 克里姆林宫城墙
actor-gagate-a-name = 边境检查站

## civilian-structures.yaml
actor-cafncb-name = 黑色栅栏
actor-cafncw-name = 白色栅栏
actor-gasand-name = 沙袋
actor-cafncp-name = 战俘营栅栏
actor-cawt01-name = 水塔
actor-cats01-name = 双筒仓
actor-cabarn02-name = 谷仓
actor-cawash01-name = 白宫
actor-cawsh12-name = 华盛顿纪念碑
actor-cawash14-name = 杰斐逊纪念堂
actor-cawash15-name = 林肯纪念堂
actor-cawash16-name = 史密森城堡
actor-cawash17-name = 史密森自然历史博物馆
actor-cawash18-name = 白宫喷泉
actor-cawash19-name = 硫磺岛纪念碑
actor-canewy04-name = 自由女神像
actor-canewy05-name = 世贸中心
actor-canewy20-name = 仓库
actor-canewy21-name = 仓库
actor-caswst01-name = 西南风格建筑
actor-caarmy01-name = 军用帐篷
actor-caarmy02-name = 军用帐篷
actor-caarmy03-name = 军用帐篷
actor-caarmy04-name = 军用帐篷
actor-cafarm01-name = 农场
actor-cafarm02-name = 农场筒仓
actor-cafarm06-name = 灯塔
actor-causfgl-name = 美国国旗
actor-carufgl-name = 俄罗斯国旗
actor-cairfgl-name = 伊拉克国旗
actor-capofgl-name = 波兰国旗
actor-caskfgl-name = 韩国国旗
actor-calbfgl-name = 利比亚国旗
actor-cafrfgl-name = 法国国旗
actor-cagefgl-name = 德国国旗
actor-cacufgl-name = 古巴国旗
actor-caukfgl-name = 英国国旗
actor-cacolo01-name = 空军学院教堂
actor-caind01-name = 工厂
actor-calab-name = 爱因斯坦实验室
actor-cagas01-name = 加油站
actor-galite-name = 路灯
actor-city05-name = 巴特西发电站
actor-catech01-name = 通讯中心
actor-catexs02-name = 阿拉莫
actor-capars01-name = 埃菲尔铁塔
actor-capars07-name = 电话亭
actor-capars10-name = 小酒馆
actor-capars11-name = 凯旋门
actor-capars12-name = 巴黎圣母院
actor-capars13-name = 小酒馆
actor-capars14-name = 小酒馆
actor-cafrma-name = 农舍
actor-cafrmb-name = 户外茅房
actor-caprs03-name = 卢浮宫
actor-cagard01-name = 警卫亭
actor-carus01-name = 圣瓦西里大教堂
actor-carus02g-name = 克里姆林宫钟楼
actor-carus03-name = 克里姆林宫
actor-camiam04-name = 救生员岗亭
actor-camiam08-name = 亚利桑那纪念馆
actor-camex01-name = 玛雅金字塔
actor-camex02-name = 玛雅城堡
actor-camex03-name = 玛雅小神庙
actor-camex04-name = 玛雅大神庙
actor-camex05-name = 玛雅平台
actor-caeur1-name = 农舍
actor-caeur2-name = 农舍
actor-cachig04-name = 联合中心
actor-cachig05-name = 西尔斯大厦
actor-cachig06-name = 水塔
actor-castl05a-name = 体育场
actor-castl05b-name = 体育场
actor-castl05c-name = 体育场
actor-castl05d-name = 体育场
actor-castl05e-name = 体育场
actor-castl05f-name = 体育场
actor-castl05g-name = 体育场
actor-castl05h-name = 体育场
actor-camsc07-name = 小屋
actor-camsc08-name = 小屋
actor-camsc09-name = 小屋
actor-camsc10-name = 汉堡王
actor-cabunk01-name = 混凝土碉堡
actor-cabunk02-name = 混凝土碉堡

## civilian-vehicles.yaml
actor-bus-name = 校车
actor-limo-name = 总统专车
actor-pick-name = 皮卡
actor-car-name = 轿车
actor-wini-name = 房车
actor-propa-name = 宣传车
actor-cop-name = 警车
actor-euroc-name = 欧式轿车
actor-cona-name = 挖掘机
actor-trucka-name = 卡车
actor-truckb-name = 卡车
actor-suvb-name = 黑色 SUV
actor-suvw-name = 白色 SUV

actor-stang =
    .name = 野马跑车
    .generic-name = 跑车

actor-ptruck-name = 皮卡
actor-taxi-name = 出租车

## civilians.yaml
actor-civa-name = 德州居民
actor-civb-name = 德州居民
actor-civc-name = 德州居民
actor-civbbp-name = 棒球运动员
actor-civbfm-name = 沙滩胖汉
actor-civbf-name = 沙滩胖妇
actor-civbtm-name = 沙滩瘦汉
actor-civsfm-name = 雪地胖汉
actor-civsf-name = 雪地胖汉
actor-civstm-name = 雪地瘦汉
actor-vladimir-name = 弗拉基米尔
actor-pentgen-name = 五角大楼将军
actor-ssrv-name = 特勤人员
actor-pres-name = 总统

## defaults.yaml
meta-civbuilding-name = 民用建筑

meta-civilianinfantry =
    .name = 平民
    .generic-name = 平民

meta-civvehicle-generic-name = 民用载具
meta-husk-generic-name = 飞机残骸
meta-ship-generic-name = 舰船
meta-oredrill-name = 矿石钻机
meta-tree-name = 树木
meta-streetsign-name = 路牌
meta-trafficlight-name = 红绿灯
meta-streetlight-name = 路灯
meta-rock-name = 岩石

meta-crate =
    .name = 补给箱
    .generic-name = 补给箱

## misc.yaml
actor-mpspawn-name = （多人游戏出生点）
actor-waypoint-name = （脚本行为路径点）
actor-camera-name = （为所有者揭开区域）
actor-ingalite-name = （隐形路灯）
actor-neglamp-name = （隐形负光灯）
actor-inyelwlamp-name = （隐形黄灯）
actor-inpurplamp-name = （隐形紫灯）
actor-inoranlamp-name = （隐形橙灯）
actor-ingrnlmp-name = （隐形绿灯）
actor-inredlmp-name = （隐形红灯）
actor-inblulmp-name = （隐形蓝灯）

## soviet-infantry.yaml
actor-e2 =
    .description =
    廉价的步枪步兵。
      擅长对抗步兵
      不擅长对抗载具、飞机
    .name = 动员兵

actor-flakt =
    .description =
    防空 / 反步兵单位。
      擅长对抗飞机、步兵
      不擅长对抗载具
    .name = 防空步兵

actor-shk =
    .description =
    使用电能的特种装甲步兵。
      擅长对抗步兵、轻型装甲
      不擅长对抗坦克、飞机
    特殊能力：为磁爆线圈充能
    .name = 磁爆步兵

actor-terror =
    .description =
    身绑 C4 炸药，以自杀式袭击
    快速高效地炸毁敌人。
      擅长对抗地面单位
      不擅长对抗飞机
    .name = 恐怖分子

actor-deso =
    .description =
    携带辐射武器，
    可部署造成范围伤害。
      擅长对抗步兵、轻型装甲
      不擅长对抗坦克、飞机
    .name = 辐射工兵

actor-ivan =
    .description = 爆破专家，可在任何东西上安放炸弹，包括奶牛。
    .name = 疯狂伊文

actor-civan =
    .description = 爆破专家，可在任何东西上安放炸弹，包括奶牛。还可传送至地图任意位置。
    .name = 超时空伊文

actor-yuri =
    .description =
    心灵步兵，可心灵控制敌方单位，
    可部署释放强大的心灵冲击波。
      擅长对抗步兵、载具
      不擅长对抗恐怖机器人、飞机、建筑
    .name = 尤里

actor-yuripr =
    .description =
    心灵步兵，可从极远距离心灵控制敌方单位，
    可部署释放强大的心灵冲击波。
      擅长对抗步兵、载具
      不擅长对抗恐怖机器人、飞机、建筑

    最多可训练 1 名。
    .name = 尤里X

## soviet-naval.yaml
actor-sapc =
    .description =
    多用途海上运输，
    可运载步兵与载具。
      无武装
    .name = 两栖运输艇

actor-sub =
    .description =
    水下反舰单位，配备鱼雷，
    可侦测其他潜艇与巨型乌贼。
      擅长对抗舰船
      不擅长对抗地面单位、飞机
    特殊能力：下潜
    .name = 台风级攻击潜艇

actor-hyd =
    .description =
    防空 / 反步兵海军单位。
      擅长对抗飞机、步兵
      不擅长对抗载具、海军单位
    .name = 海蝎

actor-sqd =
    .description =
    海洋生物，
    近身痛击敌人。
      擅长对抗舰船
    .name = 巨型乌贼

## soviet-structures.yaml
actor-nacnst =
    .description = 建造基地建筑。
    .name = 建造厂

actor-napowr =
    .description = 为其他建筑提供电力。
    .name = 磁能反应堆

actor-nahand =
    .description = 生产步兵。
    .name = 兵营

actor-narefn =
    .description = 将矿石加工为资金。
    .name = 矿石精炼厂

actor-naradr =
    .description = 提供雷达。
    .name = 雷达塔

actor-naweap =
    .description = 生产载具。
    .name = 战车工厂

actor-nayard =
    .name = 海军船坞
    .description =
    生产并维修舰船、
    潜艇、运输艇等海军单位。

actor-nadept =
    .description = 维修载具并移除恐怖机器人（收费）。
    .name = 维修厂

actor-nanrct =
    .description = 为其他建筑提供电力。
    .name = 核反应堆

actor-natech =
    .description = 解锁高级单位。
    .name = 作战实验室

actor-naclon =
    .description = 克隆大多数受训步兵。
    .name = 克隆缸

actor-napsis =
    .description = 侦测敌方单位与攻击目标点
    .name = 心灵探测器

actor-nairon =
    .description =
    使装甲单位无敌，
    对血肉之躯则是致命的。
    .name = 铁幕装置
    .grantexternalconditionpower-ironcurtain-name = 铁幕
    .grantexternalconditionpower-ironcurtain-description =
    使一组单位在 20 秒内
    处于无敌状态。

actor-namisl =
    .description =
    提供原子弹，
    需要电力才能运作。
      特殊能力：原子弹
    最多可建造 1 座。
    .name = 核弹发射井
    .nukepower-name = 核弹
    .nukepower-description =
    向目标地点发射毁灭性的
    原子弹。

actor-nawall =
    .description =
    轻型围墙，
    可被载具碾压。
    .name = 苏联围墙

actor-naflak =
    .description = 自动化防空防御。
    .name = 防空炮

actor-tesla =
    .description =
    高级基地防御，
    需要电力才能运作。
      擅长对抗步兵、载具
      不擅长对抗飞机
    .name = 磁爆线圈

actor-nalasr =
    .description = 自动化反步兵防御。
    .name = 哨戒炮

## soviet-vehicles.yaml
actor-smcv =
    .description = 可展开为建造厂。
    .name = 基地车

actor-harv =
    .description =
    采集矿石。
      擅长对抗步兵
      不擅长对抗载具、飞机
    .name = 武装采矿车

actor-dron =
    .name = 恐怖机器人
    .description =
    擅长对抗步兵、载具
      不擅长对抗飞机

actor-htk =
    .name = 防空履带车
    .description =
    步兵运输与防空 / 反步兵载具。
      擅长对抗飞机、步兵
      不擅长对抗载具

actor-htnk =
    .name = 犀牛坦克
    .description =
    苏联主战坦克。
      擅长对抗载具
      不擅长对抗步兵、飞机

actor-apoc =
    .name = 天启坦克
    .description =
    苏联高级主战坦克，配备双管主炮
    与防空导弹发射器。
      擅长对抗载具、飞机
      不擅长对抗步兵

actor-ttnk =
    .name = 磁能坦克
    .description =
    苏俄特种坦克，配备双联小型磁爆线圈。
      擅长对抗载具、步兵
      不擅长对抗飞机

actor-dtruck =
    .description = 自爆卡车，主动装载核爆炸物。
    .name = 自爆卡车

## tech-structures.yaml
actor-caoild-name = 科技油井

actor-caairp =
    .name = 科技机场
    .paratrooperspower-allies-name = 盟军伞兵
    .paratrooperspower-allies-description =
    一架运输机在地图任意位置
    空投 6 名美国大兵。
    .paratrooperspower-soviets-name = 苏军伞兵
    .paratrooperspower-soviets-description =
    一架运输机在地图任意位置
    空投 9 名动员兵。

actor-cahosp-name = 民用医院
actor-cathosp-name = 科技医院
actor-caoutp-name = 科技前哨站

## world.yaml
meta-baseworld =
    .faction-random-name = 随机
    .faction-random-description =
    随机国家
    游戏开始时将随机选择一个国家。
    .faction-allies-name = 盟军
    .faction-allies-description =
    随机盟军国家
    游戏开始时将随机选择一个盟军国家。
    .faction-soviets-name = 苏军
    .faction-soviets-description =
    随机苏军国家
    游戏开始时将随机选择一个苏军国家。
    .faction-yuri-name = 尤里
    .faction-yuri-description =
    尤里军团
    .faction-1-name = 美国
    .faction-1-description =
    美国
    特殊能力：伞兵
    .faction-2-name = 德国
    .faction-2-description =
    德国
    特殊载具：坦克杀手
    .faction-3-name = 英国
    .faction-3-description =
    英国
    特殊步兵：狙击手
    .faction-4-name = 法国
    .faction-4-description =
    法国
    特殊建筑：巨炮
    .faction-5-name = 韩国
    .faction-5-description =
    韩国
    特殊飞机：黑鹰战机
    .faction-6-name = 古巴
    .faction-6-description =
    古巴
    特殊步兵：恐怖分子
    .faction-7-name = 利比亚
    .faction-7-description =
    利比亚
    特殊载具：自爆卡车
    .faction-8-name = 伊拉克
    .faction-8-description =
    伊拉克
    特殊步兵：辐射工兵
    .faction-9-name = 俄罗斯
    .faction-9-description =
    俄罗斯
    特殊载具：磁能坦克
actor-v3 =
    .name = V3 火箭发射车
    .description =
    苏联远程炮兵载具。
    发射大型弹道火箭，火箭可被防空武器拦截。
      擅长对抗建筑、防御设施
      不擅长对抗载具、飞机

actor-v3rocket =
    .name = V3 火箭

actor-dred =
    .name = 无畏级战舰
    .description =
    苏联重型轰炸舰。
    发射两枚大型弹道导弹，导弹可被防空武器拦截。
      擅长对抗建筑、防御设施
      不擅长对抗舰船、飞机

actor-dmisl =
    .name = 无畏舰导弹

resource-minerals = 贵重矿石

actor-yacnst =
    .name = 建造场
    .description = 可建造尤里基地建筑。

actor-yapowr =
    .name = 生化反应炉
    .description =
    为其他建筑供电。
    供电量随建筑血量缩放。

actor-yabrck =
    .name = 兵营
    .description = 训练尤里步兵。

actor-yarefn =
    .name = 奴隶矿场
    .description =
    将矿石与宝石加工为资金。
    自带五名奴隶，可收起变为载具。
    选中奴隶后右键矿石即可采矿。

actor-yaweap =
    .name = 战争工厂
    .description = 生产尤里载具。

actor-yadome =
    .name = 心灵感应器
    .description = 提供雷达并解锁高级单位。

actor-yawall =
    .name = 壁垒墙
    .description = 阻挡单位并抵挡敌方火力。

actor-yaggun =
    .name = 加特林炮台
    .description =
    反步兵基地防御。
      擅长对抗步兵
      不擅长对抗坦克、飞机

actor-init =
    .name = 新兵
    .description =
    基础尤里步兵，使用心灵攻击。
      擅长对抗步兵
      不擅长对抗载具、飞机

actor-brute =
    .name = 狂兽人
    .description =
    重型近战步兵。
      擅长对抗步兵、轻型载具
      不擅长对抗坦克、飞机

actor-slav =
    .name = 奴隶

actor-pcv =
    .name = 机动建设车
    .description =
    可部署为尤里建造场。
      特殊能力：部署

actor-smin =
    .name = 奴隶矿车
    .description =
    机动提炼厂。可部署为奴隶矿场。
      特殊能力：部署

actor-ltnk =
    .name = 狂风坦克
    .description =
    快速轻型坦克。
      擅长对抗载具
      不擅长对抗步兵、飞机

actor-ytnk =
    .name = 加特林坦克
    .description =
    反步兵与防空坦克。
      擅长对抗步兵、飞机
      不擅长对抗坦克

actor-yatech =
    .name = 作战实验室
    .description = 解锁尤里高级科技。

actor-yagrnd =
    .name = 粉碎机
    .description =
    将单位回收为资金，
    并可修理载具。

actor-yadept =
    .name = 服务站
    .description = 修理载具与海军单位。

actor-yayard =
    .name = 船坞
    .description = 生产尤里海军单位，必须建在水上。

actor-yapsyt =
    .name = 心灵控制塔
    .description =
    心灵控制附近敌军单位，
    最多同时控制 3 个单位。
      擅长对抗步兵、载具
      不擅长对抗飞机、建筑

actor-natbnk =
    .name = 坦克碉堡
    .description = 可驻入一辆载具进行防护。

actor-yagntc =
    .name = 基因转化仪
    .description =
    超级武器：将目标区域的步兵转化为己方狂兽人。
    最多建造 1 座。
    .power-name = 基因转化
    .power-description = 将步兵转化为己方狂兽人。

actor-yappet =
    .name = 心灵控制器
    .description =
    超级武器：夺取单位并破坏建筑。
    最多建造 1 座。
    .power-name = 心灵控制
    .power-description = 夺取敌军单位并重创建筑。

actor-virus =
    .name = 病毒狙击手
    .description =
    狙击手步兵，发射毒素飞镖。
      擅长对抗步兵
      不擅长对抗载具、飞机

actor-caos =
    .name = 狂乱无人车
    .description =
    部署后释放狂乱毒气，使附近单位陷入狂乱并优先攻击友军。
      特殊能力：部署

actor-tele =
    .name = 磁力震慑波
    .description =
    吸起并拖动载具与舰船，同时以磁力光束伤害建筑。
      擅长对抗载具、建筑
      不擅长对抗步兵、飞机

actor-mind =
    .name = 主宰坦克
    .description =
    可同时心灵控制多个敌军单位。
      擅长对抗步兵、载具
      不擅长对抗飞机、建筑

actor-disk =
    .name = 漂浮碟
    .description =
    悬浮攻击飞行器，使用激光武器。
      擅长对抗地面、飞机
      不擅长对抗防空

actor-yhvr =
    .name = 气垫运输艇
    .description =
    两栖运输单位。
      特殊能力：运输

actor-bsub =
    .name = 飞弹潜艇
    .description =
    配备鱼雷与弹道导弹的潜艇。
      擅长对抗舰船、地面
      不擅长对抗反潜

actor-bmisl =
    .name = 飞弹潜艇导弹
