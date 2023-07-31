DELETE FROM `weenie` WHERE `class_Id` = 1000000;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (1000000, 'ace1000000-consignmentvendor', 12, '2023-01-04 08:45:39') /* Vendor */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (1000000,   1,         16) /* ItemType - Creature */
     , (1000000,   2,         31) /* CreatureType - Human */
     , (1000000,   6,         -1) /* ItemsCapacity */
     , (1000000,   7,         -1) /* ContainersCapacity */
     , (1000000,  16,         32) /* ItemUseable - Remote */
     , (1000000,  25,         11) /* Level */
     , (1000000,  74, 2147483647) /* MerchandiseItemTypes */
     , (1000000,  75,          0) /* MerchandiseMinValue */
     , (1000000,  76,          0) /* MerchandiseMaxValue */
     , (1000000,  93,    2098200) /* PhysicsState - ReportCollisions, IgnoreCollisions, Gravity, ReportCollisionsAsEnvironment */
     , (1000000, 113,          2) /* Gender - Female */
     , (1000000, 133,          4) /* ShowableOnRadar - ShowAlways */
     , (1000000, 134,         16) /* PlayerKillerStatus - RubberGlue */
     , (1000000, 188,          2) /* HeritageGroup - Gharundim */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (1000000,   1, True ) /* Stuck */
     , (1000000,  19, False) /* Attackable */
     , (1000000,  39, True ) /* DealMagicalItems */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (1000000,  37,       0) /* BuyPrice */
     , (1000000,  38,       1) /* SellPrice */
     , (1000000,  54,       3) /* UseRadius */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (1000000,   1, 'Consignment Vendor') /* Name */
     , (1000000,   5, 'Consignment Vendor') /* Template */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (1000000,   1, 0x0200004E) /* Setup */
     , (1000000,   2, 0x09000001) /* MotionTable */
     , (1000000,   3, 0x20000002) /* SoundTable */
     , (1000000,   6, 0x0400007E) /* PaletteBase */
     , (1000000,   8, 0x06001036) /* Icon */
     , (1000000,   9, 0x05001056) /* EyesTexture */
     , (1000000,  10, 0x05001074) /* NoseTexture */
     , (1000000,  11, 0x05001096) /* MouthTexture */
     , (1000000,  15, 0x04001FE1) /* HairPalette */
     , (1000000,  16, 0x040004AE) /* EyesPalette */
     , (1000000,  17, 0x040002B5) /* SkinPalette */;

INSERT INTO `landblock_instance` (`weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`)
VALUES (1000000, 0x7D64013A, 93.92, 84.70, 15.205, 0.7131, 0, 0, 0.7011, 0x00);

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (1000000,   1, 110, 0, 0) /* Strength */
     , (1000000,   2,  95, 0, 0) /* Endurance */
     , (1000000,   3, 120, 0, 0) /* Quickness */
     , (1000000,   4, 100, 0, 0) /* Coordination */
     , (1000000,   5, 120, 0, 0) /* Focus */
     , (1000000,   6, 100, 0, 0) /* Self */;

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (1000000,   1,   181, 0, 0,  228) /* MaxHealth */
     , (1000000,   3,   180, 0, 0,  275) /* MaxStamina */
     , (1000000,   5,    90, 0, 0,  190) /* MaxMana */;

INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (1000000, 4, 1000002, -1, 0,    0, False) /* Create Consignment Box (1000001) for Shop */;


DELETE FROM `weenie` WHERE `class_Id` = 1000001;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (1000001, 'ace34726-consignmenttable', 10, '2023-01-06 07:58:09') /* Creature */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (1000001,   1,         16) /* ItemType - Creature */
     , (1000001,   5,       1000) /* EncumbVal */
     , (1000001,   6,         -1) /* ItemsCapacity */
     , (1000001,   7,         -1) /* ContainersCapacity */
     , (1000001,  16,         32) /* ItemUseable - Remote */
     , (1000001,  19,          0) /* Value */
     , (1000001,  93,    6292504) /* PhysicsState - ReportCollisions, IgnoreCollisions, Gravity, ReportCollisionsAsEnvironment, EdgeSlide */
     , (1000001,  95,          3) /* RadarBlipColor - White */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (1000001,   1, True ) /* Stuck */
     , (1000001,  19, False) /* Attackable */
     , (1000001,  52, True ) /* AiImmobile */
     , (1000001,  79, True ) /* AiAcceptEverything */
     , (1000001,  82, True ) /* DontTurnOrMoveWhenGiving */
     , (1000001,  83, True ) /* NpcLooksLikeObject */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (1000001,  39,     0.7) /* DefaultScale */
     , (1000001,  54,       2) /* UseRadius */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (1000001,   1, 'Consignment Table') /* Name */
     , (1000001,  15, 'List items for sale by giving Consignment Boxes to the table.') /* ShortDesc */
     , (1000001,  16, 'You must first inscribe the Consignment Box with a price (numbers only).') /* LongDesc */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (1000001,   1, 0x020010A3) /* Setup */
     , (1000001,   2, 0x0900015B) /* MotionTable */
     , (1000001,   3, 0x2000008C) /* SoundTable */
     , (1000001,   8, 0x0600334B) /* Icon */
     , (1000001,  22, 0x3400002A) /* PhysicsEffectTable */;

INSERT INTO `landblock_instance` (`weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`)
VALUES (1000001, 0x7D64013A, 93.49, 82.76, 15.205, -0.9998, 0, 0, 0., 0x00);



DELETE FROM `weenie` WHERE `class_Id` = 1000002;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (1000002, 'ace1000002-consignmentbox', 21, '2023-01-04 08:42:12') /* Container */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (1000002,   1,        512) /* ItemType - Container */
     , (1000002,   5,        100) /* EncumbranceVal */
     , (1000002,   6,          1) /* ItemsCapacity */
     , (1000002,  16,         56) /* ItemUseable - ContainedViewedRemote */
     , (1000002,  19,       5000) /* Value */
     , (1000002,  93,       1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (1000002,   2, False) /* Open */
     , (1000002,  22, True ) /* Inscribable */
     , (1000002,  34, False) /* DefaultOpen */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (1000002,  39,    0.75) /* DefaultScale */
     , (1000002,  54,     0.5) /* UseRadius */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (1000002,   1, 'Consignment Box') /* Name */
     , (1000002,  14, 'Inscribe this Consignment Box with a price, then hand it to a Consignment Table to list the contained item for sale.') /* Use */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (1000002,   1, 0x0200011E) /* Setup */
     , (1000002,   3, 0x20000014) /* SoundTable */
     , (1000002,   6, 0x04000BEF) /* PaletteBase */
     , (1000002,   8, 0x060012F8) /* Icon */
     , (1000002,  22, 0x3400002B) /* PhysicsEffectTable */
     , (1000002,  50, 0x0600285C) /* IconOverlay */;


DELETE FROM `weenie` WHERE `class_Id` = 1000003;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (1000003, 'ace1000003-consignmentpaymentsack', 21, '2023-01-04 08:42:12') /* Container */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (1000003,   1,        512) /* ItemType - Container */
     , (1000003,   5,         10) /* EncumbranceVal */
     , (1000003,   6,         24) /* ItemsCapacity */
     , (1000003,  16,         56) /* ItemUseable - ContainedViewedRemote */
     , (1000003,  19,          1) /* Value */
     , (1000003,  93,       1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (1000003,   2, False) /* Open */
     , (1000003,  22, True ) /* Inscribable */
     , (1000003,  34, False) /* DefaultOpen */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (1000003,  39,    0.75) /* DefaultScale */
     , (1000003,  54,     0.5) /* UseRadius */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (1000003,   1, 'Consignment Payments') /* Name */
     , (1000003,  14, 'This sack holds your payments from consignment sales') /* Use */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (1000003,   1, 0x02000181) /* Setup */
     , (1000003,   3, 0x20000014) /* SoundTable */
     , (1000003,   6, 0x04000BEF) /* PaletteBase */
     , (1000003,   8, 0x0600102C) /* Icon */
     , (1000003,  22, 0x3400002B) /* PhysicsEffectTable */
     , (1000003,  50, 0x0600229F) /* IconOverlay */;

