import '../../models/packing_entry.dart';

/// Service for packing list management, templates, and statistics.
class PackingListService {
  const PackingListService();

  /// Pre-built template items for common trip types.
  List<PackingItem> getTemplateItems(TripType tripType) {
    switch (tripType) {
      case TripType.beach: return _beachTemplate();
      case TripType.business: return _businessTemplate();
      case TripType.camping: return _campingTemplate();
      case TripType.cityBreak: return _cityBreakTemplate();
      case TripType.skiing: return _skiingTemplate();
      case TripType.roadTrip: return _roadTripTemplate();
      case TripType.backpacking: return _backpackingTemplate();
      case TripType.custom: return _essentialsTemplate();
    }
  }

  /// Overall packing progress across multiple lists.
  PackingStats computeStats(List<PackingList> lists) {
    if (lists.isEmpty) {
      return const PackingStats(totalLists: 0, completedLists: 0, totalItems: 0,
        packedItems: 0, essentialItems: 0, packedEssentials: 0, categoryBreakdown: {});
    }
    int totalItems = 0, packedItems = 0, essentialItems = 0, packedEssentials = 0, completedLists = 0;
    final catCounts = <PackingCategory, int>{};
    for (final list in lists) {
      totalItems += list.totalItems;
      packedItems += list.packedItems;
      essentialItems += list.essentialItems;
      packedEssentials += list.packedEssentials;
      if (list.isComplete) completedLists++;
      for (final item in list.items) {
        catCounts[item.category] = (catCounts[item.category] ?? 0) + 1;
      }
    }
    return PackingStats(totalLists: lists.length, completedLists: completedLists,
      totalItems: totalItems, packedItems: packedItems,
      essentialItems: essentialItems, packedEssentials: packedEssentials, categoryBreakdown: catCounts);
  }

  /// Find commonly forgotten items (unpacked essentials across lists).
  List<String> findCommonlyForgotten(List<PackingList> lists) {
    final forgottenCounts = <String, int>{};
    for (final list in lists) {
      for (final item in list.items) {
        if (item.isEssential && !item.isPacked) {
          forgottenCounts[item.name] = (forgottenCounts[item.name] ?? 0) + 1;
        }
      }
    }
    final sorted = forgottenCounts.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
    return sorted.map((e) => e.key).toList();
  }

  /// Category with the most unpacked items.
  PackingCategory? mostUnpackedCategory(PackingList list) {
    if (list.items.isEmpty) return null;
    final counts = <PackingCategory, int>{};
    for (final item in list.items) {
      if (!item.isPacked) counts[item.category] = (counts[item.category] ?? 0) + 1;
    }
    if (counts.isEmpty) return null;
    return counts.entries.reduce((a, b) => a.value >= b.value ? a : b).key;
  }

  /// Get items sorted by priority (essentials first, then by category).
  List<PackingItem> prioritizedUnpacked(PackingList list) {
    final unpacked = list.items.where((i) => !i.isPacked).toList();
    unpacked.sort((a, b) {
      if (a.isEssential != b.isEssential) return a.isEssential ? -1 : 1;
      return a.category.index.compareTo(b.category.index);
    });
    return unpacked;
  }

  /// Suggest quantities based on trip duration.
  Map<String, int> suggestQuantities(int tripDays) => {
    'Underwear': tripDays + 1,
    'Socks': tripDays + 1,
    'T-shirts': (tripDays * 0.7).ceil().clamp(2, 10),
    'Pants/Shorts': (tripDays / 3).ceil().clamp(1, 5),
    'Outfits': (tripDays / 2).ceil().clamp(1, 7),
  };

  // ── Templates ──

  List<PackingItem> _essentialsTemplate() => [
    _item('e1', 'Passport/ID', PackingCategory.documents, essential: true),
    _item('e2', 'Phone charger', PackingCategory.electronics, essential: true),
    _item('e3', 'Medications', PackingCategory.medications, essential: true),
    _item('e4', 'Wallet', PackingCategory.accessories, essential: true),
    _item('e5', 'Toothbrush', PackingCategory.toiletries, essential: true),
    _item('e6', 'Toothpaste', PackingCategory.toiletries),
    _item('e7', 'Deodorant', PackingCategory.toiletries),
    _item('e8', 'Underwear', PackingCategory.clothing, qty: 3),
    _item('e9', 'Socks', PackingCategory.clothing, qty: 3),
  ];

  List<PackingItem> _beachTemplate() => [..._essentialsTemplate(),
    _item('b1', 'Swimsuit', PackingCategory.clothing, essential: true),
    _item('b2', 'Sunscreen', PackingCategory.toiletries, essential: true),
    _item('b3', 'Sunglasses', PackingCategory.accessories),
    _item('b4', 'Beach towel', PackingCategory.gear),
    _item('b5', 'Flip flops', PackingCategory.clothing),
    _item('b6', 'Hat', PackingCategory.accessories),
    _item('b7', 'Aloe vera', PackingCategory.toiletries),
    _item('b8', 'Snorkel gear', PackingCategory.gear),
    _item('b9', 'Waterproof phone case', PackingCategory.electronics),
    _item('b10', 'Light cover-up', PackingCategory.clothing),
  ];

  List<PackingItem> _businessTemplate() => [..._essentialsTemplate(),
    _item('bz1', 'Suit/Blazer', PackingCategory.clothing, essential: true),
    _item('bz2', 'Dress shoes', PackingCategory.clothing, essential: true),
    _item('bz3', 'Laptop', PackingCategory.electronics, essential: true),
    _item('bz4', 'Laptop charger', PackingCategory.electronics, essential: true),
    _item('bz5', 'Business cards', PackingCategory.documents),
    _item('bz6', 'Dress shirts', PackingCategory.clothing, qty: 2),
    _item('bz7', 'Tie', PackingCategory.accessories),
    _item('bz8', 'Notebook & pen', PackingCategory.other),
    _item('bz9', 'Travel iron', PackingCategory.gear),
  ];

  List<PackingItem> _campingTemplate() => [..._essentialsTemplate(),
    _item('ca1', 'Tent', PackingCategory.gear, essential: true),
    _item('ca2', 'Sleeping bag', PackingCategory.gear, essential: true),
    _item('ca3', 'Flashlight', PackingCategory.gear, essential: true),
    _item('ca4', 'First aid kit', PackingCategory.medications, essential: true),
    _item('ca5', 'Water bottle', PackingCategory.food, essential: true),
    _item('ca6', 'Camp stove', PackingCategory.gear),
    _item('ca7', 'Matches/Lighter', PackingCategory.gear),
    _item('ca8', 'Bug spray', PackingCategory.toiletries),
    _item('ca9', 'Hiking boots', PackingCategory.clothing),
    _item('ca10', 'Rain jacket', PackingCategory.clothing),
    _item('ca11', 'Knife/Multi-tool', PackingCategory.gear),
    _item('ca12', 'Rope', PackingCategory.gear),
  ];

  List<PackingItem> _cityBreakTemplate() => [..._essentialsTemplate(),
    _item('cb1', 'Comfortable shoes', PackingCategory.clothing, essential: true),
    _item('cb2', 'Day backpack', PackingCategory.gear),
    _item('cb3', 'Camera', PackingCategory.electronics),
    _item('cb4', 'Guidebook/Map', PackingCategory.entertainment),
    _item('cb5', 'Umbrella', PackingCategory.accessories),
    _item('cb6', 'Portable battery', PackingCategory.electronics),
  ];

  List<PackingItem> _skiingTemplate() => [..._essentialsTemplate(),
    _item('sk1', 'Ski jacket', PackingCategory.clothing, essential: true),
    _item('sk2', 'Ski pants', PackingCategory.clothing, essential: true),
    _item('sk3', 'Thermal base layers', PackingCategory.clothing, essential: true, qty: 2),
    _item('sk4', 'Goggles', PackingCategory.gear, essential: true),
    _item('sk5', 'Gloves', PackingCategory.clothing, essential: true),
    _item('sk6', 'Helmet', PackingCategory.gear),
    _item('sk7', 'Lip balm (SPF)', PackingCategory.toiletries),
    _item('sk8', 'Hand warmers', PackingCategory.gear),
    _item('sk9', 'Neck gaiter', PackingCategory.clothing),
    _item('sk10', 'Ski socks', PackingCategory.clothing, qty: 3),
  ];

  List<PackingItem> _roadTripTemplate() => [..._essentialsTemplate(),
    _item('rt1', 'Snacks', PackingCategory.food, essential: true),
    _item('rt2', 'Water bottles', PackingCategory.food, qty: 2),
    _item('rt3', 'Car charger', PackingCategory.electronics),
    _item('rt4', 'Music playlist', PackingCategory.entertainment),
    _item('rt5', 'Pillow', PackingCategory.gear),
    _item('rt6', 'Blanket', PackingCategory.gear),
    _item('rt7', 'Trash bags', PackingCategory.other),
    _item('rt8', 'Cooler', PackingCategory.gear),
  ];

  List<PackingItem> _backpackingTemplate() => [..._essentialsTemplate(),
    _item('bp1', 'Backpack (50-70L)', PackingCategory.gear, essential: true),
    _item('bp2', 'Quick-dry towel', PackingCategory.toiletries),
    _item('bp3', 'Padlock', PackingCategory.accessories),
    _item('bp4', 'Universal adapter', PackingCategory.electronics, essential: true),
    _item('bp5', 'Rain cover', PackingCategory.gear),
    _item('bp6', 'Dry bags', PackingCategory.gear),
    _item('bp7', 'Flip flops (hostel)', PackingCategory.clothing),
    _item('bp8', 'Headlamp', PackingCategory.gear),
    _item('bp9', 'Copies of documents', PackingCategory.documents, essential: true),
    _item('bp10', 'Travel insurance docs', PackingCategory.documents, essential: true),
  ];

  PackingItem _item(String id, String name, PackingCategory cat,
      {bool essential = false, int qty = 1}) =>
    PackingItem(id: id, name: name, category: cat, quantity: qty, isEssential: essential);
}

/// Aggregate packing statistics.
class PackingStats {
  final int totalLists;
  final int completedLists;
  final int totalItems;
  final int packedItems;
  final int essentialItems;
  final int packedEssentials;
  final Map<PackingCategory, int> categoryBreakdown;

  const PackingStats({
    required this.totalLists, required this.completedLists,
    required this.totalItems, required this.packedItems,
    required this.essentialItems, required this.packedEssentials,
    required this.categoryBreakdown,
  });

  double get overallProgress => totalItems > 0 ? (packedItems / totalItems * 100) : 0;
  double get essentialProgress => essentialItems > 0 ? (packedEssentials / essentialItems * 100) : 0;
  int get unpackedItems => totalItems - packedItems;
  int get unpackedEssentials => essentialItems - packedEssentials;

  PackingCategory? get topCategory {
    if (categoryBreakdown.isEmpty) return null;
    return categoryBreakdown.entries.reduce((a, b) => a.value >= b.value ? a : b).key;
  }
}
