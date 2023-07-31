CREATE TABLE consignment (
    id int unsigned NOT NULL AUTO_INCREMENT,
    object_Id int unsigned NOT NULL COMMENT 'Id of the object this consignment is for',
    seller_Id int unsigned NOT NULL COMMENT 'Id of the character this consignment is for',
    price int unsigned NOT NULL COMMENT 'Price of the consignment',
    original_value int NOT NULL COMMENT 'Original value of the item (value gets set to price during consignment)',
    list_Time int unsigned NOT NULL COMMENT 'Unix timestamp when this consignment was listed',
    expire_Time int unsigned NOT NULL COMMENT 'Unix timestamp when this consignment will expire',
    PRIMARY KEY (id),
    UNIQUE KEY `consignment_object_unique` (`object_Id`),
    KEY `consignment_seller_idx` (`seller_Id`),
    KEY `consignment_expire_idx` (`expire_Time`),
    CONSTRAINT `FK_consignment_object_Id` FOREIGN KEY (`object_Id`) REFERENCES `biota` (`id`) ON DELETE CASCADE,
    CONSTRAINT `FK_consignment_seller_Id` FOREIGN KEY (`seller_Id`) REFERENCES `biota` (`id`) ON DELETE CASCADE
);

CREATE TABLE consignment_complete (
     id int unsigned NOT NULL,
     object_Id int unsigned NOT NULL COMMENT 'Id of the object this consignment is for',
     weenie_Class_Id int unsigned NOT NULL COMMENT 'Wcid of the weenie this consignment is for (in case the object is modified/destroyed after sale)',
     seller_Id int unsigned NOT NULL COMMENT 'Id of the character this consignment is for',
     buyer_Id int unsigned NULL,
     price int unsigned NOT NULL COMMENT 'Price of the consignment',
     sold_Time int unsigned,
     PRIMARY KEY (id),
     KEY `consignment_object_idx` (`object_Id`),
     KEY `consignment_seller_idx` (`seller_Id`),
     KEY `consignment_buyer_idx` (`buyer_Id`),
     CONSTRAINT `FK_consignment_complete_object_Id` FOREIGN KEY (`object_Id`) REFERENCES `biota` (`id`) ON DELETE NO ACTION,
     CONSTRAINT `FK_consignment_complete_seller_Id` FOREIGN KEY (`seller_Id`) REFERENCES `biota` (`id`) ON DELETE NO ACTION,
     CONSTRAINT `FK_consignment_complete_buyer_Id` FOREIGN KEY (`buyer_Id`) REFERENCES `biota` (`id`) ON DELETE NO ACTION
);
