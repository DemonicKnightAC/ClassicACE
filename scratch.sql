SELECT
    ws.value,
    iLvl.value,
    td.tier
FROM weenie w
JOIN weenie_properties_int iLvl
    ON w.class_id = iLvl.object_id
    AND iLvl.type = 25
JOIN weenie_properties_string ws
    ON w.class_id = ws.object_id
JOIN weenie_properties_d_i_d did
    ON w.class_id = did.object_id
    AND ws.type = 1
JOIN treasure_death td
    ON did.value = td.treasure_Type
    AND did.type = 35
GROUP BY
    ws.value,
    iLvl.value,
    td.tier,
    td.loot_Quality_Mod
ORDER BY
    td.tier ASC,
    td.loot_Quality_Mod DESC,
    ws.value ASC;

INTO OUTFILE '/var/lib/mysql-files/monster_tiers.csv'
FIELDS ENCLOSED BY '"'
TERMINATED BY ';'
ESCAPED BY '"'
LINES TERMINATED BY '\r\n';


SELECT
    ws.value,
    iLvl.value
FROM weenie w
JOIN weenie_properties_int iLvl
    ON w.class_id = iLvl.object_id
    AND iLvl.type = 25
JOIN weenie_properties_string ws
    ON w.class_id = ws.object_id
JOIN weenie_properties_d_i_d did
    ON w.class_id = did.object_id
    AND ws.type = 1
JOIN treasure_death td
    ON did.value = td.treasure_Type
    AND did.type = 35
WHERE td.tier = 3
GROUP BY
    ws.value,
    iLvl.value
ORDER BY
    iLvl.value ASC,
    ws.value ASC;

--Find Swords by Wield Req
SELECT
    ws.value AS Name,
    iSkill.value AS Skill,
    iReq.value AS Wield,
    ROUND(iDmg.value * fVar.value, 1) AS Min,
    iDmg.value AS Max,
    iSpd.value AS Speed,
    ROUND(fAtt.value, 2) AS Attack,
    ROUND(fDef.value, 2) AS Defense,
    COUNT(sb.spell) AS spells
FROM weenie w
JOIN weenie_properties_int iSkill
    ON w.class_id = iSkill.object_id
    AND iSkill.type = 48
JOIN weenie_properties_int iReq
    ON w.class_id = iReq.object_id
    AND iReq.type = 160
JOIN weenie_properties_int iDmg
    ON w.class_id = iDmg.object_id
    AND iDmg.type = 44
JOIN weenie_properties_int iSpd
    ON w.class_id = iSpd.object_id
    AND iSpd.type = 49
JOIN weenie_properties_float fAtt
    ON w.class_id = fAtt.object_id
    AND fAtt.type = 62
JOIN weenie_properties_float fDef
    ON w.class_id = fDef.object_id
    AND fDef.type = 29
JOIN weenie_properties_float fVar
    ON w.class_id = fVar.object_id
    AND fVar.type = 22
JOIN weenie_properties_string ws
    ON w.class_id = ws.object_id
    AND ws.type = 1
LEFT JOIN weenie_properties_spell_book sb
    ON w.class_id = sb.object_id
    WHERE iSkill.value = 11
    AND iReq.value = 350
GROUP BY w.class_id
ORDER BY
    iSkill.value,
    iReq.value,
    iDmg.value,
    w.class_id;


SELECT
    sName.value as 'Name',
    iMin.value as 'Min',
    iMax.value as 'Max',
    fBuy.value as 'Buy',
    fSell.value as 'Sell',
    td.tier as 'Tier',
    td.loot_Quality_Mod as 'Loot Quality'
FROM weenie w
JOIN weenie_properties_string sName
    ON w.class_id = sName.object_id
    AND sName.type = 1
JOIN weenie_properties_int iMin
    ON w.class_id = iMin.object_id
    AND iMin.type = 75
JOIN weenie_properties_int iMax
    ON w.class_id = iMax.object_id
    AND iMax.type = 76
JOIN weenie_properties_float fBuy
    ON w.class_id = fBuy.object_id
    AND fBuy.type = 37
JOIN weenie_properties_float fSell
    ON w.class_id = fSell.object_id
    AND fSell.type = 38
JOIN weenie_properties_generator gen
     ON w.class_id = gen.object_id
JOIN treasure_death td
     ON gen.weenie_Class_Id = td.treasure_Type
WHERE w.type = 12
ORDER BY td.tier DESC
LIMIT 100;

CREATE VIEW monsters AS
SELECT
    sName.value as 'Name',
    iLvl.value as 'Level',
    td.tier as 'Tier',
    td.loot_Quality_Mod as 'Loot Quality',
    ROUND(td.item_Min_Amount + td.item_Max_Amount / 2) as 'Drops',
    ROUND(td.magic_Item_Min_Amount + td.magic_Item_Max_Amount / 2) as 'Magic Drops',
    COALESCE(lb.tally, 0) as 'spawns',
    ROUND(COALESCE(sMagicD.init_Level, 0) + ((aFocus.init_Level + aSelf.init_Level) / 7)) as 'Magic D',
    ROUND(COALESCE(sMeleeD.init_Level, 0) + ((aCoord.init_Level + aQuick.init_Level) / 3)) as 'Melee D',
    fAS.value as 'Slash Armor',
    fAP.value as 'Pierce Armor',
    fAB.value as 'Bludge Armor',
    fAF.value as 'Fire Armor',
    fAC.value as 'Frost Armor',
    fAA.value as 'Acid Armor',
    fAL.value as 'Light Armor',
    fRS.value as 'Slash Resist',
    fRP.value as 'Pierce Resist',
    fRB.value as 'Bludge Resist',
    fRF.value as 'Fire Resist',
    fRC.value as 'Frost Resist',
    fRA.value as 'Acid Resist',
    fRL.value as 'Light Resist'
FROM weenie w
JOIN weenie_properties_int iLvl
    ON w.class_id = iLvl.object_id
    AND iLvl.type = 25
JOIN weenie_properties_attribute aCoord
    ON w.class_id = aCoord.object_id
    AND aCoord.type = 4
JOIN weenie_properties_attribute aQuick
    ON w.class_id = aQuick.object_id
    AND aQuick.type = 3
JOIN weenie_properties_attribute aFocus
    ON w.class_id = aFocus.object_id
    AND aFocus.type = 5
JOIN weenie_properties_attribute aSelf
    ON w.class_id = aSelf.object_id
    AND aSelf.type = 6
LEFT JOIN weenie_properties_skill sMagicD
    ON w.class_id = sMagicD.object_id
    AND sMagicD.type = 15
LEFT JOIN weenie_properties_skill sMeleeD
    ON w.class_id = sMeleeD.object_id
    AND sMeleeD.type = 6
JOIN weenie_properties_float fAS
    ON w.class_id = fAS.object_id
    AND fAS.type = 13
JOIN weenie_properties_float fAP
    ON w.class_id = fAP.object_id
    AND fAP.type = 14
JOIN weenie_properties_float fAB
    ON w.class_id = fAB.object_id
    AND fAB.type = 15
JOIN weenie_properties_float fAF
    ON w.class_id = fAF.object_id
    AND fAF.type = 16
JOIN weenie_properties_float fAC
    ON w.class_id = fAC.object_id
    AND fAC.type = 17
JOIN weenie_properties_float fAA
    ON w.class_id = fAA.object_id
    AND fAA.type = 18
JOIN weenie_properties_float fAL
    ON w.class_id = fAL.object_id
    AND fAL.type = 19
JOIN weenie_properties_float fRS
    ON w.class_id = fRS.object_id
    AND fRS.type = 64
JOIN weenie_properties_float fRP
    ON w.class_id = fRP.object_id
    AND fRP.type = 65
JOIN weenie_properties_float fRB
    ON w.class_id = fRB.object_id
    AND fRB.type = 66
JOIN weenie_properties_float fRF
    ON w.class_id = fRF.object_id
    AND fRF.type = 67
JOIN weenie_properties_float fRC
    ON w.class_id = fRC.object_id
    AND fRC.type = 68
JOIN weenie_properties_float fRA
    ON w.class_id = fRA.object_id
    AND fRA.type = 69
JOIN weenie_properties_float fRL
    ON w.class_id = fRL.object_id
    AND fRL.type = 70
JOIN weenie_properties_string sName
    ON w.class_id = sName.object_id
    AND sName.type = 1
JOIN weenie_properties_d_i_d didTreasure
    ON w.class_id = didTreasure.object_id
    AND didTreasure.type = 35
JOIN treasure_death td
    ON didTreasure.value = td.treasure_Type
LEFT JOIN (SELECT weenie_Class_Id, COUNT(*) as tally FROM landblock_instance GROUP BY weenie_Class_Id) lb
    ON lb.weenie_Class_Id = w.class_id
ORDER BY
    td.tier,
    iLvl.value
INTO OUTFILE '/var/lib/mysql-files/monsters_Jan_31.csv'
FIELDS ENCLOSED BY '"'
TERMINATED BY ';'
ESCAPED BY '"'
LINES TERMINATED BY '\r\n';

SELECT
    w.class_id as 'ID',
    sName.value as 'Name',
    td.tier as 'Tier',
    td.loot_Quality_Mod as 'Loot Quality',
    ROUND(td.item_Min_Amount + td.item_Max_Amount / 2) as 'Drops',
    ROUND(td.magic_Item_Min_Amount + td.magic_Item_Max_Amount / 2) as 'Magic Drops'
FROM weenie w
JOIN weenie_properties_string sName
    ON w.class_id = sName.object_id
    AND sName.type = 1
JOIN weenie_properties_int iType
    ON w.class_id = iType.object_id
    AND iType.type = 1
JOIN weenie_properties_generator gen
    ON w.class_id = gen.object_id
JOIN treasure_death td
    ON gen.weenie_Class_Id = td.treasure_Type
WHERE iType.value = 512
ORDER BY td.tier
INTO OUTFILE '/var/lib/mysql-files/chest.csv'
FIELDS ENCLOSED BY '"'
TERMINATED BY ';'
ESCAPED BY '"'
LINES TERMINATED BY '\r\n';

SELECT *
FROM weenie w
JOIN weenie_properties_skill s
    ON w.class_id = s.object_id
WHERE w.class_id = 7096;