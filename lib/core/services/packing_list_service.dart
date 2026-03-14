import '../../models/packing_entry.dart';

/// Service for packing list management, templates, and statistics.
class PackingListService {
  const PackingListService();

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

  PackingStats computeStats(List<PackingList> lists) {
    if (lists.isEmpty) return const PackingStats(totalLists: 0, completedLists: 0, totalItems: 0, packedItems: 0, essentialItems: 0, packedEssentials: 0, categoryBreakdown: {});
    int total = 0, packed = 0, essential = 0, packedEss = 0, completed = 0;
    final cats = <PackingCategory, int>{};
    for (final l in lists) {
      total += l.totalItems; packed += l.packedItems;
      essential += l.essentialItems; packedEss += l.packedEssentials;
      if (l.isComplete) completed++;
      for (final i in l.items) cats[i.category] = (cats[i.category] ?? 0) + 1;
    }
    return PackingStats(totalLists: lists.length, completedLists: completed, totalItems: total, packedItems: packed, essentialItems: essential, packedEssentials: packedEss, categoryBreakdown: cats);
  }

  List<String> findCommonlyForgotten(List<PackingList> lists) {
    final counts = <String, int>{};
    for (final l in lists) for (final i in l.items) if (i.isEssential && !i.isPacked) counts[i.name] = (counts[i.name] ?? 0) + 1;
    final sorted = counts.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
    return sorted.map((e) => e.key).toList();
  }

  PackingCategory? mostUnpackedCategory(PackingList list) {
    if (list.items.isEmpty) return null;
    final counts = <PackingCategory, int>{};
    for (final i in list.items) if (!i.isPacked) counts[i.category] = (counts[i.category] ?? 0) + 1;
    if (counts.isEmpty) return null;
    return counts.entries.reduce((a, b) => a.value >= b.value ? a : b).key;
  }

  List<PackingItem> prioritizedUnpacked(PackingList list) {
    final unpacked = list.items.where((i) => !i.isPacked).toList();
    unpacked.sort((a, b) {
      if (a.isEssential != b.isEssential) return a.isEssential ? -1 : 1;
      return a.category.index.compareTo(b.category.index);
    });
    return unpacked;
  }

  Map<String, int> suggestQuantities(int tripDays) => {
    'Underwear': tripDays + 1, 'Socks': tripDays + 1,
    'T-shirts': (tripDays * 0.7).ceil().clamp(2, 10),
    'Pants/Shorts': (tripDays / 3).ceil().clamp(1, 5),
    'Outfits': (tripDays / 2).ceil().clamp(1, 7),
  };

  List<PackingItem> _essentialsTemplate() => [
    _i('e1', 'Passport/ID', PackingCategory.documents, e: true),
    _i('e2', 'Phone charger', PackingCategory.electronics, e: true),
    _i('e3', 'Medications', PackingCategory.medications, e: true),
    _i('e4', 'Wallet', PackingCategory.accessories, e: true),
    _i('e5', 'Toothbrush', PackingCategory.toiletries, e: true),
    _i('e6', 'Toothpaste', PackingCategory.toiletries),
    _i('e7', 'Deodorant', PackingCategory.toiletries),
    _i('e8', 'Underwear', PackingCategory.clothing, q: 3),
    _i('e9', 'Socks', PackingCategory.clothing, q: 3),
  ];

  List<PackingItem> _beachTemplate() => [..._essentialsTemplate(),
    _i('b1', 'Swimsuit', PackingCategory.clothing, e: true),
    _i('b2', 'Sunscreen', PackingCategory.toiletries, e: true),
    _i('b3', 'Sunglasses', PackingCategory.accessories),
    _i('b4', 'Beach towel', PackingCategory.gear),
    _i('b5', 'Flip flops', PackingCategory.clothing),
    _i('b6', 'Hat', PackingCategory.accessories),
    _i('b7', 'Aloe vera', PackingCategory.toiletries),
    _i('b8', 'Snorkel gear', PackingCategory.gear),
    _i('b9', 'Waterproof phone case', PackingCategory.electronics),
    _i('b10', 'Light cover-up', PackingCategory.clothing),
  ];

  List<PackingItem> _businessTemplate() => [..._essentialsTemplate(),
    _i('bz1', 'Suit/Blazer', PackingCategory.clothing, e: true),
    _i('bz2', 'Dress shoes', PackingCategory.clothing, e: true),
    _i('bz3', 'Laptop', PackingCategory.electronics, e: true),
    _i('bz4', 'Laptop charger', PackingCategory.electronics, e: true),
    _i('bz5', 'Business cards', PackingCategory.documents),
    _i('bz6', 'Dress shirts', PackingCategory.clothing, q: 2),
    _i('bz7', 'Tie', PackingCategory.accessories),
    _i('bz8', 'Notebook & pen', PackingCategory.other),
    _i('bz9', 'Travel iron', PackingCategory.gear),
  ];

  List<PackingItem> _campingTemplate() => [..._essentialsTemplate(),
    _i('ca1', 'Tent', PackingCategory.gear, e: true),
    _i('ca2', 'Sleeping bag', PackingCategory.gear, e: true),
    _i('ca3', 'Flashlight', PackingCategory.gear, e: true),
    _i('ca4', 'First aid kit', PackingCategory.medications, e: true),
    _i('ca5', 'Water bottle', PackingCategory.food, e: true),
    _i('ca6', 'Camp stove', PackingCategory.gear),
    _i('ca7', 'Matches/Lighter', PackingCategory.gear),
    _i('ca8', 'Bug spray', PackingCategory.toiletries),
    _i('ca9', 'Hiking boots', PackingCategory.clothing),
    _i('ca10', 'Rain jacket', PackingCategory.clothing),
    _i('ca11', 'Knife/Multi-tool', PackingCategory.gear),
    _i('ca12', 'Rope', PackingCategory.gear),
  ];

  List<PackingItem> _cityBreakTemplate() => [..._essentialsTemplate(),
    _i('cb1', 'Comfortable shoes', PackingCategory.clothing, e: true),
    _i('cb2', 'Day backpack', PackingCategory.gear),
    _i('cb3', 'Camera', PackingCategory.electronics),
    _i('cb4', 'Guidebook/Map', PackingCategory.entertainment),
    _i('cb5', 'Umbrella', PackingCategory.accessories),
    _i('cb6', 'Portable battery', PackingCategory.electronics),
  ];

  List<PackingItem> _skiingTemplate() => [..._essentialsTemplate(),
    _i('sk1', 'Ski jacket', PackingCategory.clothing, e: true),
    _i('sk2', 'Ski pants', PackingCategory.clothing, e: true),
    _i('sk3', 'Thermal base layers', PackingCategory.clothing, e: true, q: 2),
    _i('sk4', 'Goggles', PackingCategory.gear, e: true),
    _i('sk5', 'Gloves', PackingCategory.clothing, e: true),
    _i('sk6', 'Helmet', PackingCategory.gear),
    _i('sk7', 'Lip balm (SPF)', PackingCategory.toiletries),
    _i('sk8', 'Hand warmers', PackingCategory.gear),
    _i('sk9', 'Neck gaiter', PackingCategory.clothing),
    _i('sk10', 'Ski socks', PackingCategory.clothing, q: 3),
  ];

  List<PackingItem> _roadTripTemplate() => [..._essentialsTemplate(),
    _i('rt1', 'Snacks', PackingCategory.food, e: true),
    _i('rt2', 'Water bottles', PackingCategory.food, q: 2),
    _i('rt3', 'Car charger', PackingCategory.electronics),
    _i('rt4', 'Music playlist', PackingCategory.entertainment),
    _i('rt5', 'Pillow', PackingCategory.gear),
    _i('rt6', 'Blanket', PackingCategory.gear),
    _i('rt7', 'Trash bags', PackingCategory.other),
    _i('rt8', 'Cooler', PackingCategory.gear),
  ];

  List<PackingItem> _backpackingTemplate() => [..._essentialsTemplate(),
    _i('bp1', 'Backpack (50-70L)', PackingCategory.gear, e: true),
    _i('bp2', 'Quick-dry towel', PackingCategory.toiletries),
    _i('bp3', 'Padlock', PackingCategory.accessories),
    _i('bp4', 'Universal adapter', PackingCategory.electronics, e: true),
    _i('bp5', 'Rain cover', PackingCategory.gear),
    _i('bp6', 'Dry bags', PackingCategory.gear),
    _i('bp7', 'Flip flops (hostel)', PackingCategory.clothing),
    _i('bp8', 'Headlamp', PackingCategory.gear),
    _i('bp9', 'Copies of documents', PackingCategory.documents, e: true),
    _i('bp10', 'Travel insurance docs', PackingCategory.documents, e: true),
  ];

  PackingItem _i(String id, String name, PackingCategory cat, {bool e = false, int q = 1}) =>
    PackingItem(id: id, name: name, category: cat, quantity: q, isEssential: e);
}

class PackingStats {
  final int totalLists, completedLists, totalItems, packedItems, essentialItems, packedEssentials;
  final Map<PackingCategory, int> categoryBreakdown;

  const PackingStats({required this.totalLists, required this.completedLists,
    required this.totalItems, required this.packedItems,
    required this.essentialItems, required this.packedEssentials, required this.categoryBreakdown});

  double get overallProgress => totalItems > 0 ? (packedItems / totalItems * 100) : 0;
  double get essentialProgress => essentialItems > 0 ? (packedEssentials / essentialItems * 100) : 0;
  int get unpackedItems => totalItems - packedItems;
  int get unpackedEssentials => essentialItems - packedEssentials;

  PackingCategory? get topCategory {
    if (categoryBreakdown.isEmpty) return null;
    return categoryBreakdown.entries.reduce((a, b) => a.value >= b.value ? a : b).key;
  }
}
